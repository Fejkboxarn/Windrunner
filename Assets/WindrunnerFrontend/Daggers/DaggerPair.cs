using UnityEngine;
namespace Windrunner.Daggers
{
    [CreateAssetMenu(menuName="Windrunner/Daggers/Pair")]
    public sealed class DaggerPair : ScriptableObject
    {
        public string id, displayName;
        [TextArea] public string description;
        public Sprite icon;
        public GameObject leftModel, rightModel;
        public Vector3 leftOffset, rightOffset, leftEuler, rightEuler;
        public Color accent=Color.white;
        public ParticleSystem activationVfx;
        public string unlockContentId;
        public DaggerActiveEffect active;
        public DaggerPassiveEffect[] passives=System.Array.Empty<DaggerPassiveEffect>();
    }
}
