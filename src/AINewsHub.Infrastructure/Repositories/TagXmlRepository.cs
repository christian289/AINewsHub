using System.Xml;
using System.Xml.Linq;
using AINewsHub.Core.Entities;

namespace AINewsHub.Infrastructure.Repositories;

/// <summary>
/// Repository for managing tags in XML file format
/// Supports atomic writes for data integrity
/// </summary>
public class TagXmlRepository
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public TagXmlRepository(string filePath)
    {
        _filePath = filePath;
        EnsureFileExists();
    }

    private void EnsureFileExists()
    {
        if (!File.Exists(_filePath))
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("Tags",
                    new XAttribute("LastUpdated", DateTime.UtcNow.ToString("O"))
                )
            );

            SaveAtomically(doc);
        }
    }

    public IEnumerable<Tag> GetAllTags()
    {
        lock (_lock)
        {
            var doc = XDocument.Load(_filePath);
            var tagsElement = doc.Element("Tags");
            if (tagsElement == null) return [];

            return tagsElement.Elements("Tag").Select(e => new Tag
            {
                Id = int.Parse(e.Attribute("Id")?.Value ?? "0"),
                Name = e.Element("Name")?.Value ?? string.Empty,
                Category = e.Element("Category")?.Value,
                UsageCount = int.Parse(e.Element("UsageCount")?.Value ?? "0"),
                CreatedAt = DateTime.Parse(e.Element("CreatedAt")?.Value ?? DateTime.UtcNow.ToString("O"))
            }).ToList();
        }
    }

    public Tag? GetTagByName(string name)
    {
        return GetAllTags().FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public Tag? GetTagById(int id)
    {
        return GetAllTags().FirstOrDefault(t => t.Id == id);
    }

    public Tag AddTag(string name, string? category = null)
    {
        lock (_lock)
        {
            var doc = XDocument.Load(_filePath);
            var tagsElement = doc.Element("Tags");
            if (tagsElement == null)
            {
                tagsElement = new XElement("Tags");
                doc.Add(tagsElement);
            }

            // Get next ID
            var maxId = tagsElement.Elements("Tag")
                .Select(e => int.Parse(e.Attribute("Id")?.Value ?? "0"))
                .DefaultIfEmpty(0)
                .Max();

            var tag = new Tag
            {
                Id = maxId + 1,
                Name = name,
                Category = category,
                UsageCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            var tagElement = new XElement("Tag",
                new XAttribute("Id", tag.Id),
                new XElement("Name", tag.Name),
                new XElement("Category", tag.Category ?? string.Empty),
                new XElement("UsageCount", tag.UsageCount),
                new XElement("CreatedAt", tag.CreatedAt.ToString("O"))
            );

            tagsElement.Add(tagElement);
            tagsElement.SetAttributeValue("LastUpdated", DateTime.UtcNow.ToString("O"));

            SaveAtomically(doc);

            return tag;
        }
    }

    public void UpdateTagUsageCount(int tagId, int usageCount)
    {
        lock (_lock)
        {
            var doc = XDocument.Load(_filePath);
            var tagsElement = doc.Element("Tags");
            if (tagsElement == null) return;

            var tagElement = tagsElement.Elements("Tag")
                .FirstOrDefault(e => e.Attribute("Id")?.Value == tagId.ToString());

            if (tagElement != null)
            {
                var usageElement = tagElement.Element("UsageCount");
                if (usageElement != null)
                {
                    usageElement.Value = usageCount.ToString();
                }

                tagsElement.SetAttributeValue("LastUpdated", DateTime.UtcNow.ToString("O"));
                SaveAtomically(doc);
            }
        }
    }

    public void IncrementTagUsage(int tagId)
    {
        var tag = GetTagById(tagId);
        if (tag != null)
        {
            UpdateTagUsageCount(tagId, tag.UsageCount + 1);
        }
    }

    public Tag GetOrCreateTag(string name, string? category = null)
    {
        var existing = GetTagByName(name);
        if (existing != null)
            return existing;

        return AddTag(name, category);
    }

    public void DeleteTag(int tagId)
    {
        lock (_lock)
        {
            var doc = XDocument.Load(_filePath);
            var tagsElement = doc.Element("Tags");
            if (tagsElement == null) return;

            var tagElement = tagsElement.Elements("Tag")
                .FirstOrDefault(e => e.Attribute("Id")?.Value == tagId.ToString());

            if (tagElement != null)
            {
                tagElement.Remove();
                tagsElement.SetAttributeValue("LastUpdated", DateTime.UtcNow.ToString("O"));
                SaveAtomically(doc);
            }
        }
    }

    /// <summary>
    /// Saves the XML document atomically by writing to a temp file first
    /// </summary>
    private void SaveAtomically(XDocument doc)
    {
        var tempPath = _filePath + ".tmp";
        var backupPath = _filePath + ".bak";

        // Write to temp file
        using (var writer = XmlWriter.Create(tempPath, new XmlWriterSettings
        {
            Indent = true,
            Encoding = System.Text.Encoding.UTF8
        }))
        {
            doc.Save(writer);
        }

        // Backup existing file
        if (File.Exists(_filePath))
        {
            File.Copy(_filePath, backupPath, overwrite: true);
        }

        // Replace original with temp
        File.Move(tempPath, _filePath, overwrite: true);

        // Clean up backup
        if (File.Exists(backupPath))
        {
            File.Delete(backupPath);
        }
    }
}
