//////////////////////////////////////////////////////////////////////////////
 README.txt
 v1.0.2
//////////////////////////////////////////////////////////////////////////////

1. How to use
  1.1. Put the file with the following configuration.
    Assets
    |- SimpleH2downloader
       |-README.txt                  -- This file.
       |-LICENSE                     -- License for SimpleH2Downloader.
       |-ALL_LICENSES.txt            -- Licenses for Open Source Softwares used internally(contains SimpleH2Downloader).
       |-WWWAssetBundle.cs           -- H2d wrapper that behaves like a WWW.
       |-Plugins
         |-H2d.cs                    -- DLL accessor.
         |-Android                   -- below for Android.
         | |-libs
         |   |-arm64-v8a
         |   | |-libc++_shared.so
         |   | |-libh2downloader.so
         |   |-armeabi-v7a
         |   | |-libc++_shared.so
         |   | |-libh2downloader.so
         |   |-x86
         |   | |-libc++_shared.so
         |   | |-libh2downloader.so
         |-iOS                       -- below for iOS.
         | |-h2downloader.framework
         |-OSX                       -- below for macOS.
         | |-h2downloader_osx.bundle
         |-x86                       -- below for Windows(32bit)
         | |-h2downloader.dll
         |-x86_64                    -- below for Windows(64bit)
           |-h2downloader.dll
  1.2. Initialize by call H2d::dlboot method.
       Accepts the following parameters.
           string sourceAssetBundleURL
           string tmpDir
           ProxyresolverFunc proxyresolver
           int maxconn
  1.3. Terminate by call H2d::dlshutdown method.
       If you forget this, it will freeze!
  1.4. Request by call H2d::dlreq method.
       Accept the following parameter.
           string url   -- This is a relative path from sourceAssetBundleURL.
  1.5. Check response by call H2d::dlres method.
       Accepts the follow parameters.
           IntPtr presult   -- 0 is succeeded or failed.
           IntPtr pfilebuf  -- Stored the downloaded temporary file name.
           int buflen       -- Buffer length.
           IntPtr premain   -- The number of finished IDs remain.
       Return id that is done or 0
  1.6. Check progress by call H2d::dlprogress method.
       Accept the follow parameter.
           int id
       Returns the number of bytes received.
  1.7. To Change max connection, call H2d::dlsetmaxconn method.

2. FAQ
  2.1. What is the timeout?
       connection time out is 10 seconds. and returns an error when not receiving 1 byte for 4 seconds.
  2.2. How many receive buffers? 
       16KB per connection.
  2.3. What should I do to check the server certificate?
       This asset is for download, so we've skipped server certificate validation.
  2.4. Does it support H2c(HTTP/2 cleartext TCP)?
       Not supported. yet.
  2.5. Do you support server push?
       Not supported.

3. Support
  3.1. If you find any problems or bug, please contact us using the information below.
       neco@darkdrive.net

4. Revision history
  1.0.2:
    Split AUTHORS.txt to LICENSE and ALL_LICENSES.txt.
  1.0.1:
    Add AUTHORS.txt.
  1.0.0:
    First Release.

5. License
MIT (part of SimpleH2Downloader, see ALL_LICENSES.txt for others)

[EOF]
