import re

with open('OverlayForm.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Extract menuHtml
menuHtml_match = re.search(r'string menuHtml = """\s*(.*?)\s*"""\s*;', content, re.DOTALL)
if not menuHtml_match:
    print("Could not find menuHtml")
    exit(1)
menuHtml = menuHtml_match.group(1)

# Extract htmlContent
htmlContent_match = re.search(r'string htmlContent = @"(<!DOCTYPE html>.*?)";', content, re.DOTALL)
if not htmlContent_match:
    print("Could not find htmlContent")
    exit(1)
htmlContent = htmlContent_match.group(1).replace('""', '"')

# Combine
final_html = htmlContent.replace("__MENU_HTML__", menuHtml)

with open('menu.html', 'w', encoding='utf-8') as f:
    f.write(final_html)

# Replace in OverlayForm.cs
replacement = """            string htmlContent;
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("WhipCast.menu.html"))
            using (var reader = new System.IO.StreamReader(stream))
            {
                htmlContent = reader.ReadToEnd();
            }
            htmlContent = htmlContent.Replace("__STREAM_URL__", streamUrl);"""

pattern = r'string menuHtml = """.*?htmlContent = htmlContent\.Replace\("__STREAM_URL__", streamUrl\)\.Replace\("__MENU_HTML__", menuHtml\);'
new_content = re.sub(pattern, replacement, content, flags=re.DOTALL)

with open('OverlayForm.cs', 'w', encoding='utf-8') as f:
    f.write(new_content)

print("Extraction and replacement successful.")
