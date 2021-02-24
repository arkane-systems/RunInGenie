CONTAINER_RUNTIME ?= podman

.PHONY: containerized
containerized:
	rm -rf bin/ obj/
	${CONTAINER_RUNTIME} build -t dotnet-build -f images/Dockerfile.fedora .
	${CONTAINER_RUNTIME} run --name runingenie-build dotnet-build sh -c "dotnet restore && dotnet publish -c Release -r win-x64"
	${CONTAINER_RUNTIME} cp runingenie-build:/app/bin .
	${CONTAINER_RUNTIME} rm runingenie-build
	#${CONTAINER_RUNTIME} rmi dotnet-build  # this forces a rebuild of the toolchain each time
