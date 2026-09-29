using UnityEngine;
using System.Collections.Generic;
using INab.UI;
using UnityEngine.UI;

public class ProceduralProgressBarsCanvasDemo : MonoBehaviour
{
    public float lossAmount = 0.22f;
    public float fillAmount = 0.15f;

    public ProceduralProgressBar bar;
    public Image barImage;
    public List<Material> materials = new List<Material>();
    private int currentMaterialIndex = 0;

    void Start()
    {
        if (materials.Count > 0)
            ApplyMaterial();
    }

    void Update()
    {
        // Fill
        if (Input.GetKeyDown(KeyCode.E))
            bar.BarFill(fillAmount);

        // Loss
        if (Input.GetKeyDown(KeyCode.Q))
            bar.BarLoss(lossAmount);

        // Previous material
        if (Input.GetKeyDown(KeyCode.A))
        {
            currentMaterialIndex--;
            if (currentMaterialIndex < 0)
                currentMaterialIndex = materials.Count - 1;

            ApplyMaterial();
        }

        // Next material
        if (Input.GetKeyDown(KeyCode.D))
        {
            currentMaterialIndex++;
            if (currentMaterialIndex >= materials.Count)
                currentMaterialIndex = 0;

            ApplyMaterial();
        }
    }

    void ApplyMaterial()
    {
        if (materials.Count == 0 || bar == null || barImage == null) return;

        Material newMat = materials[currentMaterialIndex];
        bar.progressBarMaterial = newMat;
        barImage.material = newMat;

        bar.UpdateBarFillAmount(bar.FillAmount);
    }
}
