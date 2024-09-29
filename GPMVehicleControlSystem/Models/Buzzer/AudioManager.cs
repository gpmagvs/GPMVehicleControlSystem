
namespace GPMVehicleControlSystem.Models.Buzzer
{
    public static class AudioManager
    {

        public static string soundsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "param/sounds");

        internal static void Delete(string audioName)
        {
            string fileFullname = Path.Combine(soundsFolder, audioName);
            File.Delete(fileFullname);
        }

        internal static List<AudioInformation> GetAudiosInDisk()
        {
            return Directory.GetFiles(soundsFolder).Select(file => new AudioInformation
            {
                fileFullPath = file
            }).ToList();
        }

        internal static async Task HandleAudioUpload(IFormFile file)
        {
            var filePath = Path.Combine(soundsFolder, file.FileName);

            if (File.Exists(filePath))
            {
                //Directory.GetFiles(folder)
                filePath = _GetUniqueName(filePath);
            }

            using (FileStream stream = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                await file.CopyToAsync(stream);
            }
        }

        private static string _GetUniqueName(string filePath)
        {
            string _filePath = filePath;
            string fileNameOnly = Path.GetFileNameWithoutExtension(_filePath);
            string targetFolder = Path.GetDirectoryName(_filePath);
            string extension = Path.GetExtension(_filePath);
            int count = 1;
            while (File.Exists(_filePath))
            {
                string tempFileName = $"{fileNameOnly}({count}){extension}";
                _filePath = Path.Combine(targetFolder, tempFileName);
                count++;
            }
            //paralle coding
            return _filePath;
        }
    }

    public class AudioInformation
    {
        public string fileName => Path.GetFileName(fileFullPath);
        public string fileFullPath { get; set; } = string.Empty;
    }
}
