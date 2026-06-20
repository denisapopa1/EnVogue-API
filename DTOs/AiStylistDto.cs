using System.Text.Json.Serialization;

namespace WardrobeApi.DTOs
{
    public class AiStylistResponseDto
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("suggestions")]
        public List<AiStylistSuggestionDto> Suggestions { get; set; } = new();
    }

    public class AiStylistSuggestionDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("items")]
        public List<AiStylistItemDto> Items { get; set; } = new();

        [JsonPropertyName("collage_url")]
        public string? CollageUrl { get; set; }
    }

    public class AiStylistItemDto
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("image")]
        public string Image { get; set; } = string.Empty;
    }

    public class SaveAiOutfitDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Occasion { get; set; }
        public List<AiGeneratedClothingItemDto> Items { get; set; } = new();
    }

    public class AiGeneratedClothingItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string? CategoryName { get; set; }
        public string? SubcategoryName { get; set; }
        public string? ImagePath { get; set; }
    }
}
