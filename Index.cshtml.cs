using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace HangersAndSupportsPrompts.Pages
{
    public class IndexModel : PageModel
    {
        public string Prompt { get; set; }
        public string Snippet { get; set; }
        public string Example { get; set; }
        public string UploadResult { get; set; }

        [BindProperty]
        public IFormFile UploadedFile { get; set; }

        public async Task OnPostAsync()
        {
            var action = Request.Form["action"];
            if (action == "upload")
                await HandleFileUpload();
            else
                HandlePromptSearch();
        }

        private void HandlePromptSearch()
        {
            Prompt = Request.Form["Prompt"].ToString().Trim();
            string path = @"E:\HangersAndSupportsAI\HangersAndSupportsPrompts\snippets\snippets.txt";

            string Normalize(string s) => s.Replace(" ", "").ToLowerInvariant();

            if (System.IO.File.Exists(path) && !string.IsNullOrEmpty(Prompt))
            {
                var lines = System.IO.File.ReadAllLines(path);
                var normalizedPrompt = Normalize(Prompt);
                var snippetBuilder = new StringBuilder();
                var exampleBuilder = new StringBuilder();

                string bestHeader = null;
                int bestScore = int.MaxValue;
                var headerLines = new List<string>();

                // First pass: find the best matching header using edit distance or containment
                foreach (var line in lines)
                {
                    if (line.Trim().StartsWith("#"))
                    {
                        var normHeader = Normalize(line.Trim());
                        if (normHeader.Contains(normalizedPrompt) || normalizedPrompt.Contains(normHeader))
                        {
                            bestHeader = line.Trim();
                            break; // exact or strong partial match found
                        }

                        // Optional: Use edit distance to rank headers
                        int score = ComputeLevenshteinDistance(normHeader, normalizedPrompt);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestHeader = line.Trim();
                        }
                    }
                }

                if (bestHeader == null)
                {
                    Snippet = $"❌ No snippet found for \"{Prompt}\"";
                    return;
                }

                // Second pass: extract content for the matched header
                bool foundPrompt = false;
                bool collectingExample = false;

                foreach (var line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("#"))
                    {
                        if (foundPrompt && !trimmedLine.Equals("#example", StringComparison.OrdinalIgnoreCase))
                            break;

                        if (!foundPrompt && trimmedLine.Equals(bestHeader, StringComparison.OrdinalIgnoreCase))
                        {
                            foundPrompt = true;
                            collectingExample = false;
                            continue;
                        }

                        if (foundPrompt && trimmedLine.Equals("#example", StringComparison.OrdinalIgnoreCase))
                        {
                            collectingExample = true;
                            continue;
                        }
                    }
                    else if (foundPrompt)
                    {
                        if (collectingExample)
                            exampleBuilder.AppendLine(line);
                        else
                            snippetBuilder.AppendLine(line);
                    }
                }

                Snippet = snippetBuilder.ToString().Trim();
                Example = exampleBuilder.ToString().Trim();
            }
        }

        // Helper method: Levenshtein distance
        private int ComputeLevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int[,] d = new int[s.Length + 1, t.Length + 1];

            for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
            for (int j = 0; j <= t.Length; j++) d[0, j] = j;

            for (int i = 1; i <= s.Length; i++)
            {
                for (int j = 1; j <= t.Length; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost
                    );
                }
            }

            return d[s.Length, t.Length];
        }


        private async Task HandleFileUpload()
        {
            if (UploadedFile != null && UploadedFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var filePath = Path.Combine(uploadsFolder, Path.GetFileName(UploadedFile.FileName));
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await UploadedFile.CopyToAsync(stream);
                }

                UploadResult = $"✅ File '{UploadedFile.FileName}' uploaded successfully.";
                var ext = Path.GetExtension(filePath).ToLower();

                if (ext == ".txt")
                {
                    var text = await System.IO.File.ReadAllTextAsync(filePath);
                    UploadResult += "\n\n📑 Content:\n" + text;
                }
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    UploadResult += "\n🖼️ Image uploaded (analysis not yet implemented).";
                }
                else if (ext == ".pdf" || ext == ".docx")
                {
                    UploadResult += "\n📄 Uploaded file detected, but content preview for this type is not yet supported.";
                }
                else
                {
                    UploadResult += "\n⚠️ Unknown file type.";
                }
            }
            else
            {
                UploadResult = "⚠️ No file selected.";
            }
        }
    }
}
