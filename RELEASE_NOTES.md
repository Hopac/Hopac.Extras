#### 0.1.10 - 26.01.2015
* fixed: ProcessRunner does not dispose Process object
* fixed memory leak: File.startReading starts forever live `fileExists` job which prevents GC to collect FileReader.

#### 0.1.9 - 24.01.2015
* Update Hopac to version 0.0.0.38
* remove unnecessary delays in IO

#### 0.1.8 - 23.01.2015
* Fixed: ProcessRunner and File's DisposeAsync return not delayed jobs which causes immediate disposing on construction

#### 0.1.7 - 22.01.2015
* Add some Hopac-friendly operations on files
* Simplify ProcessRunner API

#### 0.1.6 - 21.01.2015
* ProcessRunner considers non-zero exit code as error

#### 0.1.5 - 21.01.2015
* ProcessRunner uses the Either monad for error handling

#### 0.1.4 - 21.01.2015
* Enrich ProcessRunner interface
* Add comments

#### 0.1.3 - 21.01.2015
* Remove Hopac binaries
* add Hopac NuGet package dependency

#### 0.1.2 - 18.01.2015
* ObjectPool implements IAsyncDisposable for safe blocking disposing

#### 0.1.1 - 17.01.2015
* Initial version
