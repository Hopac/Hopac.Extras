#### 0.1.30 - 26.05.2014
* fixed: ObjectPool stop responding if `creator` raises exception `capacity` number of times

#### 0.1.29 - 26.05.2015
* Update Hopac to version 0.0.0.46

#### 0.1.28 - 10.04.2015
* Update Hopac to version 0.0.0.45

#### 0.1.27 - 05.03.2015
* Add Semaphore

#### 0.1.26 - 02.03.2015
* Update Hopac to version 0.0.0.44

#### 0.1.25 - 28.02.2015
* Update Hopac to version 0.0.0.43

#### 0.1.24 - 27.02.2015
* Update Hopac to version 0.0.0.42

#### 0.1.23 - 26.02.2015
* Add ObjectPool.WithInstanceJobChoice

#### 0.1.22 - 26.02.2015
* ObjectPool.WithInstanceJob cache exceptions and transform them to Choice

#### 0.1.21 - 26.02.2015
* ObjectPool handle instance creator exceptions

#### 0.1.20 - 19.02.2015
* Fixed: File.startReading does not react on new lines appeared in a file

#### 0.1.19 - 11.02.2015
* Update Hopac to version 0.0.0.41

#### 0.1.18 - 10.02.2015
* Replace File.startReading implementation

#### 0.1.17 - 10.02.2015
* Add IO.Console

#### 0.1.16 - 09.02.2015
* Fix JobChoice and add tests for it

#### 0.1.15 - 06.02.2015
* Add JobChoice computation expression

#### 0.1.14 - 04.02.2015
* use IVar.tryFill in Process.Exited event handler since it may be called several times

#### 0.1.13 - 04.02.2015
* ObjectPool does not require instances to implement IDisposable

#### 0.1.12 - 02.02.2015
* ObjectPool ignores any exceptions in instance's Dispose

#### 0.1.11 - 01.02.2015
* fixed: ParallelExecutor hangs if a worker returns Recoverable error
* update to Hopac 0.0.0.39

#### 0.1.10 - 26.01.2015
* fixed: ProcessRunner does not dispose Process object
* fixed memory leak: File.startReading starts forever live `fileExists` job which prevents GC to collect FileReader

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
