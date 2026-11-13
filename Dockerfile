# ==============================================================================
# Railbound Netcode Dedicated Server Dockerfile
# Optimized for Unity 2023.1 LTS Linux Dedicated Server (Direct3D 12 / Null Gfx)
# ==============================================================================

FROM ubuntu:22.04

# Prevent interactive prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Install essential runtime shared libraries required by Unity Linux standalone builds
RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    libc6 \
    libgcc-s1 \
    libstdc++6 \
    libglu1-mesa \
    libxcursor1 \
    libxrandr2 \
    procps \
    && rm -rf /var/lib/apt/lists/*

# Create working directory and dedicated non-root user
RUN useradd -m -u 1000 -s /bin/bash unity && \
    mkdir -p /server/logs && \
    chown -R unity:unity /server

WORKDIR /server

# Copy pre-built Linux Dedicated Server binaries into image
COPY --chown=unity:unity Builds/Server/Linux/ /server/

# Ensure the executable has execute permissions
RUN chmod +x /server/RailboundServer.x86_64 || true

USER unity

# Expose Netcode UnityTransport default port (UDP)
EXPOSE 7777/udp

# Healthcheck to verify process is alive
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
  CMD pgrep -f RailboundServer.x86_64 > /dev/null || exit 1

# Launch headless Netcode Dedicated Server on port 7777
# "-logFile -" writes the Unity log to stdout, so `kubectl logs` shows it (a log file inside the pod is lost when the pod goes away)
ENTRYPOINT ["/server/RailboundServer.x86_64", "-batchmode", "-nographics", "-server", "-port", "7777", "-logFile", "-"]

