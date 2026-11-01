using System;
using UnityEngine;

namespace Vigilante8.Content
{
    /// <summary>
    /// Holds the original unclassified sound samples and their original indices.
    /// It does not infer whether a sound is an engine, weapon, or impact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OriginalAudioBank : MonoBehaviour
    {
        [Serializable]
        public sealed class Sample
        {
            public int originalIndex;
            public AudioClip clip;
        }

        [SerializeField] private string sourceId;
        [SerializeField] private Sample[] samples = Array.Empty<Sample>();

        public string SourceId => sourceId;
        public int Count => samples == null ? 0 : samples.Length;

        public bool TryGetOriginalSample(int index, out AudioClip clip)
        {
            if (samples != null)
            {
                foreach (Sample sample in samples)
                {
                    if (sample != null && sample.originalIndex == index && sample.clip != null)
                    {
                        clip = sample.clip;
                        return true;
                    }
                }
            }
            clip = null;
            return false;
        }

#if UNITY_EDITOR
        public void Configure(string id, Sample[] originalSamples)
        {
            if (string.IsNullOrWhiteSpace(id) || originalSamples == null ||
                originalSamples.Length == 0)
                throw new ArgumentException("Authentic source audio samples are required");
            var known = new System.Collections.Generic.HashSet<int>();
            foreach (Sample sample in originalSamples)
                if (sample == null || sample.originalIndex < 0 || sample.clip == null ||
                    !known.Add(sample.originalIndex))
                    throw new ArgumentException("Original sound bank contains missing or repeated samples");
            sourceId = id;
            samples = originalSamples;
        }
#endif
    }
}
