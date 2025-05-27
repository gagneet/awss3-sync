using System.IO;

namespace AWSS3Sync.Core.Utils // Changed namespace
{
    public class Misc // Made class public
    {
        public static string GetContentType(string fileExtension)
        {
            string contentType = string.Empty;
            switch (fileExtension.ToLower()) // Added ToLower() for case-insensitivity
            {
                case ".bmp": contentType = "image/bmp"; break;
                case ".jpeg": contentType = "image/jpeg"; break;
                case ".jpg": contentType = "image/jpeg"; break; // Corrected jpg to image/jpeg
                case ".gif": contentType = "image/gif"; break;
                case ".tiff": contentType = "image/tiff"; break;
                case ".png": contentType = "image/png"; break;
                case ".txt": contentType = "text/plain"; break; // Added .txt
                case ".plain": contentType = "text/plain"; break;
                case ".rtf": contentType = "text/rtf"; break;
                case ".doc": contentType = "application/msword"; break; // Added .doc
                case ".docx": contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"; break; // Added .docx
                case ".msword": contentType = "application/msword"; break;
                case ".zip": contentType = "application/zip"; break;
                case ".mpeg": contentType = "audio/mpeg"; break;
                case ".pdf": contentType = "application/pdf"; break;
                case ".gz": contentType = "application/x-gzip"; break; // Added .gz
                case ".xgzip": contentType = "application/x-gzip"; break;
                case "xcompressed": contentType = "application/x-compressed"; break; // Note: typo in original 'applicatoin'
                default: contentType = "application/octet-stream"; break; // Default content type
            }
            return contentType;
        }

        public static void SaveStreamToFile(string fileFullPath, Stream stream)
        {
            // The original code had 'using (stream)' which is fine if the stream should be disposed here.
            // If the stream is passed and might be used later, the caller should manage its lifecycle.
            // For now, keeping the original behavior.
            using (stream) 
            {
                using (FileStream fs = new FileStream(fileFullPath, FileMode.Create, FileAccess.Write))
                {
                    byte[] data = new byte[32768];
                    int bytesRead = 0;
                    do
                    {
                        bytesRead = stream.Read(data, 0, data.Length);
                        fs.Write(data, 0, bytesRead);
                    }
                    while (bytesRead > 0);
                    fs.Flush();
                }
            }
        }
    }
}
