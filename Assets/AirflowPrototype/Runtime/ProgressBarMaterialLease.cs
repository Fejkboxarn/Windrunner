using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AirflowPrototype
{
    /// <summary>
    /// Owns the runtime material used by the optional progress-bar plugin.
    /// Canvas Image.material is shared, unlike Renderer.material. Keep authored
    /// assets immutable without changing the plugin or its animation API.
    /// </summary>
    public sealed class ProgressBarMaterialLease : IDisposable
    {
        private readonly MonoBehaviour bar;
        private readonly FieldInfo materialField;
        private readonly Material original;
        private readonly Graphic graphic;
        private readonly Material originalGraphic;
        private readonly Renderer renderer;
        private readonly Material originalRenderer;
        private Material instance;

        public ProgressBarMaterialLease(MonoBehaviour bar)
        {
            this.bar = bar;
            if (bar == null) return;
            materialField = bar.GetType().GetField("progressBarMaterial", BindingFlags.Public | BindingFlags.Instance);
            if (materialField == null || materialField.FieldType != typeof(Material)) return;
            original = materialField.GetValue(bar) as Material;
            if (original == null) return;
            instance = new Material(original) { name = original.name + " (Windrunner runtime)", hideFlags = HideFlags.DontSave };
            materialField.SetValue(bar, instance);
            graphic = bar.GetComponent<Graphic>();
            if (graphic != null)
            {
                originalGraphic = graphic.material;
                graphic.material = instance;
            }
            var rendererField = bar.GetType().GetField("barRenderer", BindingFlags.Public | BindingFlags.Instance);
            renderer = rendererField?.GetValue(bar) as Renderer;
            if (renderer != null)
            {
                originalRenderer = renderer.sharedMaterial;
                renderer.sharedMaterial = instance;
            }
        }

        public void Dispose()
        {
            if (instance == null) return;
            if (bar != null && ReferenceEquals(materialField.GetValue(bar), instance))
                materialField.SetValue(bar, original);
            if (graphic != null && graphic.material == instance) graphic.material = originalGraphic;
            if (renderer != null && renderer.sharedMaterial == instance) renderer.sharedMaterial = originalRenderer;
            UnityEngine.Object.Destroy(instance);
            instance = null;
        }
    }
}
