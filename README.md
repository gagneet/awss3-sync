# awss3-sync
Utility in .NET with WinForms to enable users to synchronize, upload or download files from AWS S3

- I was trying to find something which is generic across the OS, but started off with this on using WinForms and .NET 4.7.2.
- This code does the basic work and error handling.
- Feel free to use it and enhance it to your needs. Would love it, if you got back and merged the fork back here.

## Create AWS Secrets file and add to the solution, before building it

- You need to create a new JSON file called: "appsettings.json"

```json
{
  "AWS": {
    "AccessKey": "AKRD5ODU6FDGS2WEG363",
    "SecretKey": "CiMSax0Qd3r8hgjitT2XmDwB9CHD9Ya0FWo/deSy",
    "Region": "ap-southeast-2",
    "BucketName": "documents"
  }
}
```

## Missing / TODO

- Need to separate the different functionality into their own files and use those to run