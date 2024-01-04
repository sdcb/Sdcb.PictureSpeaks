using Sdcb.PictureSpeaks.Services.AI.AzureOpenAI;

namespace Sdcb.PictureSpeaks.Services.AI;

public record ImageRequest(string Prompt)
{
    public ImageGenerationOptions ToOpenAI()
    {
        return new ImageGenerationOptions(Prompt, "1792x1024");
    }
}
