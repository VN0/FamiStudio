// VRC7 audio chip emulator for FamiStudio.
// Added to Nes_Snd_Emu by @NesBleuBleu, using the YM2413 emulator by Mitsutaka Okazaki.

#include "Nes_Vrc7.h"
#include "emu2413.h"

#include BLARGG_SOURCE_BEGIN

Nes_Vrc7::Nes_Vrc7() : opll(NULL), output_buffer(NULL)
{
	output(NULL);
	volume(1.0);
	reset();
}

Nes_Vrc7::~Nes_Vrc7()
{
	if (opll) 
		OPLL_delete(opll);
}

void Nes_Vrc7::reset()
{
	reg = 0;
	silence = false;
	reset_opll();
}

void Nes_Vrc7::volume(double v)
{
	// Taken from FamiTracker
	vol = v * 4.46; 
}

void Nes_Vrc7::reset_opll()
{
	if (opll)
		OPLL_delete(opll);

	opll = OPLL_new(vrc7_clock, output_buffer ? output_buffer->sample_rate() : 44100);
	OPLL_reset(opll);
	OPLL_setChipMode(opll, 1); // VRC7 mode.
	OPLL_resetPatch(opll, OPLL_VRC7_TONE); // Use VRC7 default instruments.
	OPLL_setMask(opll, ~0x3f); // Only 6 channels.
}

void Nes_Vrc7::output(Blip_Buffer* buf)
{
	output_buffer = buf;

	if (output_buffer && (!opll || output_buffer->sample_rate() != opll->rate))
		reset_opll();
}

void Nes_Vrc7::write_register(cpu_time_t time, cpu_addr_t addr, int data)
{
	switch (addr)
	{
	case reg_silence:
		silence = (data & 0x40) != 0;
		break;
	case reg_select:
		reg = data;
		break;
	case reg_write:
		OPLL_writeReg(opll, reg, data);
		break;
	}
}

void Nes_Vrc7::end_frame(cpu_time_t time)
{
	if (!output_buffer || silence)
		return;

	int sample_cnt = output_buffer->count_samples(time);
	require(sample_cnt < array_count(sample_buffer));

	for (int i = 0; i < sample_cnt; i++)
	{
		int sample = OPLL_calc(opll);
		sample = clamp(sample, -3200, 3600);
		sample = clamp((int)(sample * vol), -32768, 32767);
		sample_buffer[i] = (int16_t)sample;
	}

	output_buffer->mix_samples(sample_buffer, sample_cnt);
}
