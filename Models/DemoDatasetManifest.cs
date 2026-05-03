namespace WardrobeApi.Models
{
    public class DemoDatasetManifest
    {
        public string Name { get; set; } = "";
        public string GeneratedOn { get; set; } = "";
        public int ItemCount { get; set; }
        public List<DemoDatasetItem> Items { get; set; } = new();
    }

    public class DemoDatasetItem
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Subcategory { get; set; } = "";
        public string Season { get; set; } = "";
        public string Style { get; set; } = "";
        public string Color { get; set; } = "";
        public string Brand { get; set; } = "";
        public string RelativeImagePath { get; set; } = "";
    }

    public class DemoDatasetSeedResult
    {
        public int Total { get; set; }
        public int Imported { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public string Message { get; set; } = "";
    }
}
