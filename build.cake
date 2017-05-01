/////////////////////////////////////////////////////////////////////
// ADDINS
/////////////////////////////////////////////////////////////////////

#addin "nuget:?package=Polly&version=5.0.6"
#addin "nuget:?package=NuGet.Core&version=2.12.0"
#addin "nuget:?package=SharpZipLib&version=0.86.0"
#addin "nuget:?package=Cake.Compression&version=0.1.1"

//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

#tool "nuget:https://dotnet.myget.org/F/nuget-build/?package=NuGet.CommandLine&version=4.3.0-beta1-2361&prerelease"

///////////////////////////////////////////////////////////////////////////////
// USINGS
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using Polly;
using NuGet;

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var platform = Argument("platform", "AnyCPU");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// CONFIGURATION
///////////////////////////////////////////////////////////////////////////////

var MainRepo = "AvaloniaUI/AvaloniaEdit";
var MasterBranch = "master";
var ReleasePlatform = "Any CPU";
var ReleaseConfiguration = "Release";

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////


///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = "0.2.0";

///////////////////////////////////////////////////////////////////////////////
// DIRECTORIES
///////////////////////////////////////////////////////////////////////////////

var artifactsDir = (DirectoryPath)Directory("./artifacts");
var zipRootDir = artifactsDir.Combine("zip");
var nugetRoot = artifactsDir.Combine("nuget");
var fileZipSuffix = ".zip";

private bool MoveFolderContents(string SourcePath, string DestinationPath)
{
   SourcePath = SourcePath.EndsWith(@"\") ? SourcePath : SourcePath + @"\";
   DestinationPath = DestinationPath.EndsWith(@"\") ? DestinationPath : DestinationPath + @"\";
 
   try
   {
      if (System.IO.Directory.Exists(SourcePath))
      {
         if (System.IO.Directory.Exists(DestinationPath) == false)
         {
            System.IO.Directory.CreateDirectory(DestinationPath);
         }
 
         foreach (string files in System.IO.Directory.GetFiles(SourcePath))
         {
            FileInfo fileInfo = new FileInfo(files);
            fileInfo.MoveTo(string.Format(@"{0}\{1}", DestinationPath, fileInfo.Name));
         }
 
         foreach (string drs in System.IO.Directory.GetDirectories(SourcePath))
         {
            System.IO.DirectoryInfo directoryInfo = new DirectoryInfo(drs);
            if (MoveFolderContents(drs, DestinationPath + directoryInfo.Name) == false)
            {
               return false;
            }
         }
      }
      return true;
   }
   catch (Exception ex)
   {
      return false;
   }
}

public class ArchiveDownloadInfo
{
    public string URL {get;set;}
    public string Name {get;set;}
    public FilePath DestinationFile {get; set;}
    public string Format {get; set;}
    public Action<DirectoryPath, ArchiveDownloadInfo> PostExtract {get; set;}
}

public class ToolchainDownloadInfo
{
    private DirectoryPath _artifactsDir;

    public ToolchainDownloadInfo(DirectoryPath artifactsDir)
    {
        _artifactsDir = artifactsDir;
        Downloads = new List<ArchiveDownloadInfo>();
    }

    public DirectoryPath BaseDir {get { return _artifactsDir.Combine(RID); } }

    public DirectoryPath ZipDir {get { return _artifactsDir.Combine("zip").Combine(RID); } }

    public string RID {get; set;}
    public List<ArchiveDownloadInfo> Downloads {get; set;}
    
}

var toolchainDownloads = new List<ToolchainDownloadInfo> 
{ 
    new ToolchainDownloadInfo (artifactsDir)
    { 
        RID = "ubuntu14", 
        Downloads = new List<ArchiveDownloadInfo>()
        { 
            new ArchiveDownloadInfo()
            { 
                Format = "tar.xz", 
                DestinationFile = "clang.tar.xz", 
                URL =  "http://releases.llvm.org/4.0.0/clang+llvm-4.0.0-x86_64-linux-gnu-ubuntu-14.04.tar.xz",
                Name = "clang+llvm-4.0.0-x86_64-linux-gnu-ubuntu-14.04",
                PostExtract = (curDir, info) =>{
                    var tarFile = curDir.CombineWithFilePath("clang.tar");
                    StartProcess("7z", new ProcessSettings{ Arguments = string.Format("x {0} -o{1}", tarFile, curDir) });
                    DeleteFile(tarFile);
                    
                    Information(curDir.Combine(info.Name).ToString() + "/*.*");

                    MoveFolderContents(curDir.Combine(info.Name).ToString(), curDir.ToString());

                    DeleteDirectory(curDir.Combine(info.Name), true);
                }
            },
            new ArchiveDownloadInfo()
            {
                Format = "tar.bz2",
                DestinationFile = "gcc.bz2",
                URL = "https://developer.arm.com/-/media/Files/downloads/gnu-rm/6_1-2017q1/gcc-arm-none-eabi-6-2017-q1-update-linux.tar.bz2?product=GNU%20ARM%20Embedded%20Toolchain,64-bit,,Linux,6-2017-q1-update",
                Name= "gcc-arm-none-eabi-6-2017-q1-update",
                PostExtract = (curDir, info)=>
                {
                    MoveFolderContents(curDir.Combine(info.Name).ToString(), curDir.ToString());

                    DeleteDirectory(curDir.Combine(info.Name), true);
                }
            }
        }
    }
};


///////////////////////////////////////////////////////////////////////////////
// INFORMATION
///////////////////////////////////////////////////////////////////////////////


///////////////////////////////////////////////////////////////////////////////
// TASKS
/////////////////////////////////////////////////////////////////////////////// 

Task("Clean")
.Does(()=>{    
    foreach(var tc in toolchainDownloads)
    {
        CleanDirectory(tc.BaseDir);   
        CleanDirectory(tc.ZipDir);
    }
});

Task("Download-Toolchains")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        foreach(var downloadInfo in tc.Downloads)
        {
            var fileName = tc.ZipDir.CombineWithFilePath(downloadInfo.DestinationFile);

            Information(string.Format("./{0}/{1}", tc.ZipDir, downloadInfo.DestinationFile));

            if(!FileExists(fileName))
            {
                DownloadFile("http://releases.llvm.org/4.0.0/clang+llvm-4.0.0-x86_64-linux-gnu-ubuntu-14.04.tar.xz", string.Format("./{0}/{1}", tc.ZipDir, downloadInfo.DestinationFile));
            }
        }
    }
});

Task("Extract-Toolchains")
.Does(()=>{
    foreach(var tc in toolchainDownloads)
    {
        foreach(var downloadInfo in tc.Downloads)
        {
            var fileName = tc.ZipDir.CombineWithFilePath(downloadInfo.DestinationFile);
            var dest = tc.BaseDir;

            switch (downloadInfo.Format)
            {
                case "tar.xz":
                StartProcess("7z", new ProcessSettings{ Arguments = string.Format("e {0} -o{1}", fileName, dest) });
                break;

                case "tar.bz2":
                BZip2Uncompress(fileName, dest);
                break;
            }        

            if(downloadInfo.PostExtract != null)
            {
                downloadInfo.PostExtract(dest, downloadInfo);
            }
        }
    }
});

Task("Default")    
    .IsDependentOn("Clean")
    .IsDependentOn("Download-Toolchains")
    .IsDependentOn("Extract-Toolchains");

RunTarget(target);
