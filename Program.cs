partial class Program
{
    // check later if we can implement some async file manipulation
    static void Main(string[] args)
    {
        if (args.Length != 4 ||
            string.IsNullOrWhiteSpace(args[0]) ||
            string.IsNullOrWhiteSpace(args[1]) ||
            string.IsNullOrWhiteSpace(args[2]) ||
            string.IsNullOrWhiteSpace(args[3]))
        {
            Console.WriteLine("Please provide all necessary parameters");

            Console.WriteLine();
            Console.WriteLine("synchronizeApp [source_dir] [destination_dir] [log_file_dir] [sync_period]");
            Console.WriteLine();

            return;
        }

        // make a static ArgClass for these
        var sourcePath = args[0];
        var destPath = args[1];
        var logFilePath = args[2];

        // we expect period in seconds
        // if int is not enough long for synchronization(4.5 years) then we dont really need any sync
        var period = args[3];

        // furthermore if it is too little is possible we cannot finish previous sync
        // in this latter case might be a possible solution that we cannot make the function in loop async
        // but inside of it we might be able to manipulate files asnychronously
        if (!int.TryParse(period, out var periodSeconds))
        {
            Console.WriteLine("Period parameter should be a valid number. Program exits.");
            Console.WriteLine("synchronizeApp [source_dir] [destination_dir] [log_file_dir] [sync_period]");
            return;
        }

        // set up directories and log file for proceed if possible
        var setup = new ProcessArguments(sourcePath, destPath, logFilePath, periodSeconds);
        if (setup == null || setup.Success != true)
        {
            return;
        }

        //while (true)
        //{

        // i am going to watch Ronaldinho Gaucho's best play, anyway, i like Messi too and C.Ronaldo is a genius too
        MakeSynchronization(setup, setup.SourceDir!, setup.DestDir!, setup.LogFilePath);

        //    Thread.Sleep(setup.PeriodSeconds);
        //}


    }

    private static void MakeSynchronization(ProcessArguments setup, DirectoryInfo sourceDirInfo, DirectoryInfo destDirInfo, string logFilePath)
    {
        // collect relative filepaths from SOURCE directory and all subdirs
        var sourceDirFileList = new List<string>();
        GetFilesRelativePaths(setup, sourceDirInfo, sourceDirFileList, setup.SourcePath);

        // collect relative filepaths from DESTINATION directory and all subdirs
        var destDirfileList = new List<string>();
        GetFilesRelativePaths(setup, destDirInfo, destDirfileList, setup.DestPath, destDir: true);

        // the general idea here is that we have to find all files recursively in SOURCE and DESTINATION

        // we can spare some hash calculation if we take into account

        // 1.
        // find which filenames have a match in both directories
        // still these filenames can differ by their data,
        // for these matches we will compare their sha256 hash and if differ replace destination from source

        // 2.
        // delete those who can be found by name name in Destination but not in Source

        // at this step all those whose names are equal have really the same by data too because of hash

        // 3.
        // copy from Source to Destination all those reamining files who differ only by filename



        // of course if differ only by filename we could still make a hash comparison, but for that too
        // we have to iterate on all the file, so i am not sure that is faster to make hash then change only filename
        // than just copy all files with different names,
        // however writing to the disk is always more expensive than reading, 

        // in case we wanted to compare all files which differ only by name,
        // we have to calculate all of their hashes and comapre them all to each other, thats around n square complexity

        // in case of many files differs,
        //  i have a feeling that n square times disk reading will be soon slower than  n times disk writing

        MakeSyncByBytes(setup, sourceDirFileList, destDirfileList);

        MakeSyncByFullPaths(setup, sourceDirFileList, destDirfileList);
    }

    private static void MakeSyncByBytes(ProcessArguments setup, List<string> sourceDirFileList, List<string> destDirFileList)
    {
        var intersectPathSet = new HashSet<string>(sourceDirFileList);
        var destRelPathSet = new HashSet<string>(destDirFileList);

        intersectPathSet.IntersectWith(destRelPathSet);
        if (intersectPathSet.Count == 0)
        {
            return;
        }

        // compare intersect paths by bytes and copy source to destination if they differ

        foreach (var relPath in intersectPathSet)
        {
            var sourceFilePath = Path.Combine(setup.SourcePath, relPath);
            var destFilePath = Path.Combine(setup.DestPath, relPath);

            using var fileStream1 = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read);
            using var fileStream2 = new FileStream(destFilePath, FileMode.Open, FileAccess.Read);

            if (fileStream1.Length != fileStream2.Length)
            {
                fileStream2.Close();

                try
                {
                    File.Delete(destFilePath);

                    var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff");
                    Console.WriteLine("Delete file" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                    using var sw = new StreamWriter(setup.LogFilePath, append: true);
                    sw.WriteLine("Delete file" + " , " + "destination path: " + destFilePath + " , " + date);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error deleting destination file: " + ex.ToString());

                    var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff");
                    Console.WriteLine("Delete file" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                    using var sw = new StreamWriter(setup.LogFilePath, append: true);
                    sw.WriteLine("Exception Delete" + " , " + "destination path: " + destFilePath + " , " + date + " , " + ex.ToString());
                }

                try
                {
                    File.Copy(sourceFilePath, destFilePath);

                    var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff");
                    Console.WriteLine("Copy file" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                    using var sw = new StreamWriter(setup.LogFilePath, append: true);
                    sw.WriteLine("Copy file" + " , " + "destination path: " + destFilePath + " , " + date);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error copying source file :" + ex.ToString());

                    var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff");
                    Console.WriteLine("Exception Copy" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                    using var sw = new StreamWriter(setup.LogFilePath, append: true);
                    sw.WriteLine("Exception Copy" + " , " + "destination path: " + destFilePath + " , " + date + " , " + ex.ToString());
                }

                continue;
            }

            var bytesToRead = 1024;

            byte[] sourceBuf = new byte[bytesToRead];
            byte[] destBuf = new byte[bytesToRead];

            while (true)
            {
                var readBytesSource = fileStream1.Read(sourceBuf, 0, bytesToRead);
                var readBytesDest = fileStream2.Read(destBuf, 0, bytesToRead);

                if (readBytesSource == 0 && readBytesDest == 0)
                {
                    break;
                }

                if (sourceBuf.SequenceEqual(destBuf))
                {
                    continue;
                }

                var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff");
                fileStream2.Close();
                try
                {
                    File.Delete(destFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception Delete" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                    using var sw = new StreamWriter(setup.LogFilePath, append: true);
                    sw.WriteLine("Exception Delete" + " , " + "destination path: " + destFilePath + " , " + date + " , " + ex.ToString());
                }

                try
                {
                    File.Copy(sourceFilePath, destFilePath);

                    Console.WriteLine("Exception Delete" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                    using var sw = new StreamWriter(setup.LogFilePath, append: true);
                    sw.WriteLine("Exception Delete" + " , " + "destination path: " + destFilePath + " , " + date);
                }
                catch (Exception ex)
                {

                    Console.WriteLine("Exception Delete" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                    using var sw = new StreamWriter(setup.LogFilePath, append: true);
                    sw.WriteLine("Exception Delete" + " , " + "destination path: " + destFilePath + " , " + date + " , " + ex.ToString());
                }

                break;
            }

            // only because readability, end of cycle
            fileStream1.Close();
            fileStream2.Close();
        }
    }

    private static void GetFilesRelativePaths(ProcessArguments setup, DirectoryInfo dirInfo, List<string> fileList, string basePath, bool destDir = false)
    {
        try
        {
            var fileArray = dirInfo.GetFiles();
            if (fileArray.Length == 0)
            {
                // delete empty dir if DestDir
                if (destDir == true)
                {
                    dirInfo.Delete();
                }
                else
                
                {
                    var test = setup.DestPath + fileArray;

                    var destDirectory = new DirectoryInfo(setup.DestPath + dirInfo.Parent);
                    destDirectory.Create();
                }
                return;
            }

            foreach (var file in fileArray)
            {
                fileList.Add(Path.GetRelativePath(basePath, file.FullName));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("GetFiles failure: " + ex.ToString());
        }

        try
        {
            var dirArray = dirInfo.GetDirectories();
            if (dirArray.Length == 0)
            {
                return;
            }

            foreach (var dir in dirArray)
            {
                // mayber check for null or so
                GetFilesRelativePaths(setup, dir, fileList, basePath, destDir);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("GetDirectory failure: " + ex.ToString());
            return;
        }
    }

    private static void MakeSyncByFullPaths(ProcessArguments setup, List<string> sourceDirFileList, List<string> destDirFileList)
    {
        var sourceFileList_1 = new HashSet<string>(sourceDirFileList);
        var destFileList_1 = new HashSet<string>(destDirFileList);

        //filepaths only in source directory, we copy all without thinking
        sourceFileList_1.ExceptWith(destFileList_1);

        foreach (var file in sourceFileList_1)
        {
            var sourceFilePath = Path.Combine(setup.SourcePath, file);
            var destFilePath = Path.Combine(setup.DestPath, file);

            var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff");
            try
            {
                //check if directory exist and create if not
                if (Path.GetDirectoryName(destFilePath) is string dirName)
                {
                    Directory.CreateDirectory(dirName);
                }

                File.Copy(sourceFilePath, destFilePath);

                Console.WriteLine("File copy" + " , " + "destination path: " + destFilePath + " , " + date + "\n");

                using var sw = new StreamWriter(setup.LogFilePath, append: true);
                sw.WriteLine("File copy" + " , " + "destination path: " + destFilePath + " , " + date);

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception copy: " + ex.ToString());

                using var sw = new StreamWriter(setup.LogFilePath, append: true);
                sw.WriteLine("Exception copy" + " , " + "destination path: " + destFilePath + " , " + date + " , " + ex.ToString());

                // lets try the next file
                continue;
            }
        }

        var sourceFileList_2 = new HashSet<string>(sourceDirFileList);
        var destFileList_2 = new HashSet<string>(destDirFileList);

        // filepaths only in destination directory, we delete all without thinking
        destFileList_2.ExceptWith(sourceFileList_2);

        foreach (var file in destFileList_2)
        {
            var destFilePath = Path.Combine(setup.DestPath, file);

            var date = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss:fff");
            try
            {
                File.Delete(destFilePath);

                Console.WriteLine("Delete file" + " , " + "destination path: " + destFilePath + " , " + date);

                using var sw = new StreamWriter(setup.LogFilePath, append: true);
                sw.WriteLine("Delete file" + " , " + "destination path: " + destFilePath + " , " + date);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception Delete file" + " , " + "destination path: " + destFilePath + " , " + date + " , " + ex.ToString());

                using var sw = new StreamWriter(setup.LogFilePath, append: true);
                sw.WriteLine("Exception Delete file" + " , " + "destination path: " + destFilePath + " , " + date + " , " + ex.ToString());

                continue;
            }
        }
    }

    private class ProcessArguments
    {
        public string SourcePath
        {
            get; private set;
        }
        public string DestPath
        {
            get; private set;
        }

        public string LogFilePath
        {
            get; private set;
        }

        public bool Success
        {
            get; private set;
        }

        public int PeriodSeconds
        {
            get; private set;
        }

        public DirectoryInfo? SourceDir
        {
            get; private set;
        }
        public DirectoryInfo? DestDir
        {
            get; private set;
        }

        public ProcessArguments(string _sourcePath, string _destPath, string _logFilePath, int _periodSeconds)
        {
            SourcePath = _sourcePath;
            DestPath = _destPath;
            LogFilePath = _logFilePath;
            PeriodSeconds = _periodSeconds;

            Success = PrepareSynchronization(SourcePath, DestPath, LogFilePath);
        }

        private bool PrepareSynchronization(string sourcePath, string destPath, string logFilePath)
        {
            try
            {
                SourceDir = new DirectoryInfo(sourcePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to reach source directory: " + ex.ToString());
                Console.WriteLine("synchronizeApp [source_dir] [destination_dir] [log_file_dir] [sync_period]");
                return false;
            }

            try
            {
                DestDir = new DirectoryInfo(destPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Destination directory failure: " + ex.ToString());
                Console.WriteLine("synchronizeApp [source_dir] [destination_dir] [log_file_dir] [sync_period]");
                return false;
            }

            // if source doesnt exist then  doesnt make sense to proceed,
            // still make sense to proceed if it is empty because other program can write here files
            // so the next synchronization can have what to copy to destination
            // OK, other program still can make this directory later, but synchronization should have already a source
            if (!SourceDir.Exists)
            {
                Console.WriteLine("Source directory doesnt exist");
                return false;
            }

            if (!DestDir.Exists)
            {
                try
                {
                    DestDir.Create();
                    Console.WriteLine("Destination should have been created");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Destination directory cannot be created :" + ex.ToString());
                    return false;
                }
            }

            // check if file exists and ask input from user
            if (!SetupLogFile(logFilePath))
            {
                return false;
            }

            return true;
        }

        private static bool SetupLogFile(string logFilePath)
        {
            if (File.Exists(logFilePath))
            {
                Console.WriteLine("Log file already exists. Do you want to proceed and append new data to it? (y / n)");
                var answer = Console.ReadKey().KeyChar.ToString();
                if (answer == "y" || answer == "Y")
                {
                    try
                    {
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Creating log file failed: " + ex.ToString());
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("User didnt allow to proceed with exisitng log file. Program exits.");
                    return false;
                }
            }
            else
            {
                try
                {
                    var logFile = File.Create(logFilePath);
                    logFile.Close();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Creating log file failed: " + ex.ToString());
                    return false;
                }
            }
        }
    }
}
