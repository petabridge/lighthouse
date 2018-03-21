pushd %~dp0
REM Batch file support

if [%1]==[] (
  REM no version number passed in, get last release tag from github
  FOR /F "tokens=*" %%g IN (' git describe --abbrev=0 --tags ') DO SET release_tag=%%g

) else (
  REM can pass a target release tag e.g. bash docker-build.sh v1.0.2
  REM must include the "v" at the front of the version number e.g. "v1.0.2" NOT "1.0.2"
  SET release_tag="%~1"
)

echo "Using release tag verison %release_tag%"

docker build -t petabridge/lighthouse:netcore1.1 -t "petabridge/lighthouse:%release_tag%" -t petabridge/lighthouse:latest .

if defined DOCKER_PUSH (
	docker push petabridge/lighthouse
)