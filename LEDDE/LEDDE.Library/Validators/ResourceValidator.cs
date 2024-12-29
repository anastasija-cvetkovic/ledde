using LEDDE.Library.LED;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LEDDE.Library.Validators
{
    public class ResourceValidator
    {
        ///<summary>Proverava da li je resurs (slika ili video) validan i da li postoji</summary>
        ///<param name="resourcePath">Putanja do resursa (slike ili video fajla)</param>
        ///<param name="resourceType">Tip resursa ('image' ili 'video')</param>
        public static void ValidateResource(string resourcePath, string resourceType)
        {
            string extension = Path.GetExtension(resourcePath).ToLower();

            // Proverava da li resurs postoji
            if (!File.Exists(resourcePath))
            {
                throw new FileNotFoundException($"{resourceType} file not found.", resourcePath);
            }

            // Validacija tipa resursa
            if (resourceType != "image" && resourceType != "video")
            {
                throw new ArgumentException("Invalid resource type specified.", nameof(resourceType));
            }

            // Validacija na osnovu tipa resursa
            if (resourceType == "image" && !LEDConstants.INPUT_IMAGE_FORMATS.Contains(extension))
            {
                throw new NotSupportedException($"Unsupported image format: {extension}");
            }
            else if (resourceType == "video" && !LEDConstants.INPUT_VIDEO_FORMATS.Contains(extension))
            {
                throw new NotSupportedException($"Unsupported video format: {extension}");
            }
        }
        /*
        ///<summary>Proverava da li je slika koju želimo da emuliramo validna i da li postoji</summary>
        ///<param name="imagePath">Putanja do slike</param>
        public static void ValidateImage(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLower();

            if (!File.Exists(imagePath))
                throw new FileNotFoundException("Image file not found.", imagePath);

            if (!LEDConstants.INPUT_IMAGE_FORMATS.Contains(extension))
                throw new NotSupportedException($"Unsupported image format : {extension}");
        }
        ///<summary>Proverava da li je video koji želimo da emuliramo validan i da li postoji</summary>
        ///<param name="imagePath">Putanja do slike</param>
        public static void ValidateVideo(string imagePath)
        {
            string extension = Path.GetExtension(imagePath).ToLower();

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Video file not found.", imagePath);
            }
            if (!LEDConstants.INPUT_VIDEO_FORMATS.Contains(extension))
            {
                throw new NotSupportedException($"Unsupported video format : {extension}");
            }
        }
        */
    }
}
