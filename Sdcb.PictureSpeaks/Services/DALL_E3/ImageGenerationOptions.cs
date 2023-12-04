using System.Diagnostics.CodeAnalysis;

namespace Sdcb.PictureSpeaks.Services.DALL_E3;

public record ImageGenerationOptions
{
    [SetsRequiredMembers]
    public ImageGenerationOptions(string prompt, string size = "1024x1024") { Prompt = prompt; Size = size; }

    ///<summary>
    /// Gets or sets the text description of the desired image(s).
    /// The maximum length is 1000 characters for `dall-e-2` and 4000 characters for `dall-e-3`.
    ///</summary>
    public required string Prompt { get; init; }

    ///<summary>
    /// Gets or sets the model to use for image generation.
    ///</summary>
    public string? Model { get; init; }

    ///<summary>
    /// Gets or sets the number of images to generate. Must be between 1 and 10.
    ///</summary>
    public int? N { get; init; }

    ///<summary>
    /// Gets or sets the quality of the image that will be generated.
    ///</summary>
    public string? Quality { get; init; }

    ///<summary>
    /// Gets or sets the format in which the generated images are returned. Must be one of `url` or `b64_json`.
    ///</summary>
    public string? ResponseFormat { get; init; }

    ///<summary>
    /// Gets or sets the size of the generated images.
    ///</summary>
    public string Size { get; init; } = "1024x1024";

    ///<summary>
    /// Gets or sets the style of the generated images.
    ///</summary>
    public string? Style { get; init; }

    ///<summary>
    /// Gets or sets a unique identifier representing your end-user, which can help OpenAI to monitor and detect abuse.
    ///</summary>
    public string? User { get; init; }

    ///<summary>
    /// Gets or sets extra headers for the request.
    ///</summary>
    public string? ExtraHeaders { get; init; }

    ///<summary>
    /// Gets or sets additional query parameters to the request.
    ///</summary>
    public string? ExtraQuery { get; init; }

    ///<summary>
    /// Gets or sets additional JSON properties to the request.
    ///</summary>
    public string? ExtraBody { get; init; }

    ///<summary>
    /// Gets or sets the client-level default timeout for this request, in seconds.
    ///</summary>
    public TimeSpan? Timeout { get; init; }
}