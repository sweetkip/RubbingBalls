using System.Collections;
using UnityEngine;

public class WarmupLoader : MonoBehaviour
{
    [SerializeField] private ShaderVariantCollection shaderVariants;
    [SerializeField, Min(1)] private int variantsPerFrame = 8;
    [SerializeField, Min(1)] private int maxFrames = 600;

    private static bool done;

    private IEnumerator Start()
    {
        if (done)
            yield break;

        BallFeedback.PreloadResources();
        BallFeedbackAudio.PreloadAll();
        yield return null;

        if (shaderVariants != null)
        {
            for (int frame = 0; frame < maxFrames && !shaderVariants.isWarmedUp; frame++)
            {
                shaderVariants.WarmUpProgressively(variantsPerFrame);
                yield return null;
            }
        }

        done = true;
    }
}