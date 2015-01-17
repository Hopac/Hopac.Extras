@echo off
cls

.paket\paket.bootstrapper.exe prerelease
if errorlevel 1 (
  exit /b %errorlevel%
)

.paket\paket.exe restore -v
if errorlevel 1 (
  exit /b %errorlevel%
)

packages\FAKE\tools\FAKE.exe build.fsx "target=Release" "NugetKey=f8869f19-af3e-481b-94aa-997382b55a13" "github-user=vaskir@gmail.com"  "github-pw=newyear2011"