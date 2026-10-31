using System;
using System.Collections.Generic;

namespace DontForgetMe
{
    // This model stores character knowledge, never the real player's mental state.
    public sealed class MemoryNetwork
    {
        private sealed class Link
        {
            public string Observer;
            public string Subject;
            public float Strength;
        }

        private readonly List<Link> links = new List<Link>();

        public void Recognize(string observer, string subject)
        {
            ValidatePair(observer, subject);
            Link link = Find(observer, subject);
            if (link == null)
            {
                link = new Link { Observer = observer, Subject = subject };
                links.Add(link);
            }
            link.Strength = 1f;
        }

        public float Strength(string observer, string subject)
        {
            ValidatePair(observer, subject);
            return Find(observer, subject)?.Strength ?? 0f;
        }

        public bool IsRemembered(string subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("A subject is required.", nameof(subject));
            foreach (Link link in links)
                if (link.Subject == subject && link.Strength > 0) return true;
            return false;
        }

        public void DecayObserver(string observer, float amount)
        {
            if (string.IsNullOrWhiteSpace(observer))
                throw new ArgumentException("An observer is required.", nameof(observer));
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));
            foreach (Link link in links)
                if (link.Observer == observer)
                    link.Strength = Math.Max(0, link.Strength - amount);
        }

        private Link Find(string observer, string subject)
        {
            return links.Find(link => link.Observer == observer && link.Subject == subject);
        }

        private static void ValidatePair(string observer, string subject)
        {
            if (string.IsNullOrWhiteSpace(observer) || string.IsNullOrWhiteSpace(subject))
                throw new ArgumentException("Both memory participants are required.");
            if (observer == subject)
                throw new ArgumentException("Self-memory does not protect a character.");
        }
    }
}
