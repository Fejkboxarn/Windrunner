using UnityEngine;
using System.Collections.Generic;
using INab.UI;

public class BarsManager : MonoBehaviour
{
    public float lossAmount = 0.3f;
    public float fillAmount = 0.2f;

    /** ============================================================================ **/
    /**
    // Custom duration for the bar fill and loss animations
    // If you do not want to use the default duration, set useCustomDuration to true
    **/
    public float customDuration = 0.4f;
    public bool useCustomDuration = false;
    /** ============================================================================ **/

    public List<ProceduralProgressBar> bars = new List<ProceduralProgressBar>();

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
            foreach (var bar in bars)
            {
                if (useCustomDuration)
                {
                    // Loss with custom duration
                    bar.BarLoss(lossAmount, customDuration);
                }
                else
                {
                    // BASIC loss with default duration
                    bar.BarLoss(lossAmount);
                }

            }

        if (Input.GetKeyDown(KeyCode.E))
            foreach (var bar in bars)
            {
                if (useCustomDuration)
                {
                    // Fill with custom duration
                    bar.BarFill(fillAmount, customDuration);
                }
                else
                {
                    // BASIC fill with default duration
                    bar.BarFill(fillAmount);
                }
            }

        if (Input.GetKeyDown(KeyCode.R))
            foreach (var bar in bars)
            {
                // Update the Fill Amount directly
                bar.UpdateBarFillAmount(0.5f);

            }

    }
}
