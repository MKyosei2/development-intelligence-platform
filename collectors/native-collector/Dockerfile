FROM ubuntu:24.04 AS build
RUN apt-get update && apt-get install -y --no-install-recommends build-essential cmake
WORKDIR /src
COPY collectors/native-collector .
RUN cmake -S . -B build -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF     && cmake --build build --config Release --parallel

FROM ubuntu:24.04
WORKDIR /app
COPY --from=build /src/build/dip-native-collector /usr/local/bin/dip-native-collector
ENTRYPOINT ["dip-native-collector"]
