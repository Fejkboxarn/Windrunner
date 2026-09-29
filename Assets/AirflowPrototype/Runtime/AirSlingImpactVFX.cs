using System;
using UnityEngine;

namespace AirflowPrototype
{
    [DisallowMultipleComponent]
    public sealed class AirSlingImpactVFX : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] particleSystems;

        public event Action ImpactPlayed;

        public void Configure(
            ParticleSystem[] newParticleSystems)
        {
            particleSystems =
                newParticleSystems;
        }

        public void PlayImpact()
        {
            if (particleSystems != null)
            {
                for (int i = 0;
                     i < particleSystems.Length;
                     i++)
                {
                    ParticleSystem system =
                        particleSystems[i];

                    if (system == null)
                        continue;

                    system.Stop(
                        true,
                        ParticleSystemStopBehavior
                            .StopEmittingAndClear);

                    system.Play(
                        true);
                }
            }

            ImpactPlayed?.Invoke();
        }
    }
}
