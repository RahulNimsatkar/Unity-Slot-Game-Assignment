using System.IO;
using UnityEditor;
using UnityEngine;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Procedurally synthesizes every sound effect of the game and writes them as
    /// 16-bit PCM WAV files into Assets/Sounds.
    ///
    /// Why synthesize? The assignment ships no audio, and self-generated retro
    /// casino blips keep the project 100% license-free while adding real game feel.
    /// All sounds are deterministic (fixed seeds), so re-running the generator
    /// always produces identical files.
    /// </summary>
    public static class SoundGenerator
    {
        private const int SampleRate = 22050;

        public static void GenerateAll()
        {
            Directory.CreateDirectory(AssetPaths.SoundsFolder);

            Write("click.wav",      Click());
            Write("lever.wav",      Lever());
            Write("reel_stop.wav",  ReelStop());
            Write("spin_loop.wav",  SpinLoop());
            Write("win.wav",        Arpeggio(new[] { 523f, 659f, 784f, 1047f }, 0.09f));
            Write("jackpot.wav",    Arpeggio(new[] { 392f, 523f, 659f, 784f, 1047f, 1319f, 1568f }, 0.11f));
            Write("lose.wav",       Lose());

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SlotGame] Sound generation complete (7 clips).");
        }

        // ------------------------------------------------------------- recipes

        // Short UI click: square blip with fast decay.
        private static float[] Click()
        {
            int n = (int)(SampleRate * 0.045f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 90f);
                s[i] = (Square(880f, t) * 0.6f + Sine(1760f, t) * 0.2f) * env * 0.5f;
            }
            return s;
        }

        // Lever clunk: pitch sweep down + a noise burst for the mechanical rattle.
        private static float[] Lever()
        {
            int n = (int)(SampleRate * 0.22f);
            var s = new float[n];
            var rnd = new System.Random(7);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 18f);
                float freq = Mathf.Lerp(260f, 85f, t / 0.22f);
                float tone = Sine(freq, t) * 0.55f;
                float rattle = ((float)rnd.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-t * 40f);
                s[i] = (tone + rattle) * env;
            }
            return s;
        }

        // Reel stop: low thud with a clicky transient.
        private static float[] ReelStop()
        {
            int n = (int)(SampleRate * 0.13f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float thud = Sine(135f, t) * Mathf.Exp(-t * 35f) * 0.9f;
                float tick = Square(2400f, t) * Mathf.Exp(-t * 300f) * 0.35f;
                s[i] = (thud + tick) * 0.8f;
            }
            return s;
        }

        // Continuous reel spin hum — a buzzy low tone that loops cleanly.
        // Length is ~0.5s at SampleRate; the AudioSource loops it.
        private static float[] SpinLoop()
        {
            float dur = 0.5f;
            int n = (int)(SampleRate * dur);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                // Two detuned sawtooths for a mechanical whir.
                float saw1 = (t * 120f % 1f) * 2f - 1f;
                float saw2 = (t * 121.7f % 1f) * 2f - 1f;
                // Fade in/out at the edges for click-free looping.
                float fade = Mathf.Clamp01(Mathf.Min(i, n - i) / 200f);
                s[i] = (saw1 * 0.3f + saw2 * 0.25f) * fade * 0.35f;
            }
            return s;
        }

        // Rising arpeggio: played for wins and jackpots.
        private static float[] Arpeggio(float[] notes, float noteLen)
        {
            int noteN = (int)(SampleRate * noteLen);
            int tailN = (int)(SampleRate * 0.18f);
            int total = noteN * notes.Length + tailN;
            var s = new float[total];
            for (int ni = 0; ni < notes.Length; ni++)
            {
                float freq = notes[ni];
                for (int i = 0; i < noteN; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-t * 12f);
                    int idx = ni * noteN + i;
                    s[idx] += (Sine(freq, t) * 0.5f + Square(freq, t) * 0.18f) * env * 0.55f;
                }
            }
            // Reverb tail: mix last note ringing out.
            float lastFreq = notes[notes.Length - 1];
            int tailStart = noteN * notes.Length;
            for (int i = 0; i < tailN; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 20f);
                s[tailStart + i] = Sine(lastFreq, t) * env * 0.25f;
            }
            return s;
        }

        // Descending "wah-wah" for a loss.
        private static float[] Lose()
        {
            int n = (int)(SampleRate * 0.55f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 5f);
                float freq = Mathf.Lerp(440f, 180f, t / 0.55f);
                s[i] = Sine(freq, t) * env * 0.45f;
            }
            return s;
        }

        // --------------------------------------------------------- WAV writer

        /// <summary>Writes a mono 16-bit PCM WAV into Assets/Sounds.</summary>
        private static void Write(string filename, float[] samples)
        {
            string path = Path.Combine(AssetPaths.SoundsFolder, filename);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int byteCount = samples.Length * 2; // 16 bit = 2 bytes per sample

                // RIFF header
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + byteCount);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt chunk
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16);                // chunk size
                bw.Write((short)1);          // PCM format
                bw.Write((short)1);          // mono
                bw.Write(SampleRate);        // sample rate
                bw.Write(SampleRate * 2);    // byte rate
                bw.Write((short)2);          // block align
                bw.Write((short)16);         // bits per sample

                // data chunk
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(byteCount);
                for (int i = 0; i < samples.Length; i++)
                {
                    float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                    bw.Write((short)(clamped * 32767f));
                }
            }
            Debug.Log("[SlotGame] Generated: " + path);
        }

        // ------------------------------------------------- oscillator helpers

        private static float Sine(float freq, float t)
        {
            return Mathf.Sin(2f * Mathf.PI * freq * t);
        }

        private static float Square(float freq, float t)
        {
            return Sine(freq, t) >= 0f ? 0.8f : -0.8f;
        }
    }
}
