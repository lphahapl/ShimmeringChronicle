using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DecodeReferenceAudio
{
    public static void Run()
    {
        try
        {
            const string path = "Assets/SwordAudioReference.mp3";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = 48000;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var samples = new float[clip.samples * clip.channels];
            if (!clip.GetData(samples, 0)) throw new Exception("Reference audio decode failed");
            using (var writer = new BinaryWriter(File.Create("sword-reference.wav")))
            {
                int size = samples.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + size);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)clip.channels); writer.Write(clip.frequency);
                writer.Write(clip.frequency * clip.channels * 2); writer.Write((short)(clip.channels * 2)); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(size);
                foreach (float value in samples) writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(value, -1, 1) * 32767));
            }
            File.WriteAllText("sword-reference-info.txt", "Decoded " + clip.length.ToString("F3") + " s, " + clip.frequency + " Hz, " + clip.channels + " channel(s)");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
