# _Questions for the self-check:_

## 1. What is orchestration?

**Answer:** Orchestration is the automated management of containerized applications across multiple hosts — handling deployment, scaling, networking, health monitoring, and self-healing. Tools like Kubernetes automate the "where and how" of running containers: scheduling pods onto nodes, restarting failed containers, rolling out updates, and balancing load. In the broader sense, orchestration coordinates multiple services (containers, microservices) so they work together as a system without manual intervention.

---

## 2. What is containerization and the pros and cons of using it?

**Answer:** Containerization packages an application and all its dependencies (libraries, config, runtime) into a self-contained unit (container) that runs consistently across any environment using OS-level virtualization (shared kernel).

**Pros:**
- **Portability** — "build once, run anywhere" (dev → staging → prod, any cloud)
- **Isolation** — each container has its own filesystem, process space, and network
- **Lightweight** — containers share the host OS kernel; far less overhead than VMs
- **Fast startup** — seconds vs. minutes for VMs
- **Reproducibility** — the image is immutable; no "works on my machine" problems
- **Scalability** — easy to spin up/down instances horizontally
- **Microservices-friendly** — each service can be versioned and deployed independently

**Cons:**
- **Weaker isolation than VMs** — shared kernel means a kernel exploit can affect all containers
- **Persistent storage complexity** — containers are ephemeral; managing stateful data requires volumes or external storage
- **Networking complexity** — container networking (overlays, service discovery) adds operational overhead
- **Orchestration overhead** — running at scale requires Kubernetes or equivalent, which has significant learning curve
- **Windows/Linux compatibility** — Linux containers don't run natively on Windows without a VM layer
- **Image bloat** — poorly written Dockerfiles can produce large, slow-to-pull images

---

## 3. What is the difference between containerization and virtualization?

**Answer:**

| | Virtualization (VMs) | Containerization |
|---|---|---|
| **Isolation unit** | Full OS + kernel | Process-level (shared kernel) |
| **Hypervisor** | Required (Type 1 or 2) | Not needed (uses OS namespaces/cgroups) |
| **Startup time** | Minutes | Seconds |
| **Overhead** | High (each VM has full OS) | Low (shared kernel) |
| **Portability** | Less portable (OS-dependent images) | Highly portable |
| **Security isolation** | Stronger (separate kernel) | Weaker (shared kernel) |
| **Use case** | Running different OSes, strong isolation | Microservices, fast scaling, CI/CD |

VMs virtualize the **hardware**; containers virtualize the **OS**.

---

## 4. Explain the usage flow of Docker & Kubernetes.

**Answer:**

**Docker flow (build → ship → run):**
1. Write a `Dockerfile` describing the image (base image, dependencies, app code, entrypoint)
2. `docker build` — creates an immutable image from the Dockerfile
3. `docker push` — pushes the image to a container registry (Docker Hub, ECR, ACR, etc.)
4. `docker run` — pulls the image and starts a container from it on any host

**Kubernetes flow (deploy → manage → scale):**
1. Write YAML manifests: `Deployment`, `Service`, `ConfigMap`, `Ingress`, etc.
2. `kubectl apply -f` — Kubernetes scheduler places Pods (containers) onto Nodes
3. The **control plane** (API Server, etcd, Scheduler, Controller Manager) continuously reconciles desired state vs. actual state
4. **Kubelet** on each Node pulls the image from the registry and starts the container
5. Services expose Pods via stable DNS/IP; Ingress handles external traffic
6. Horizontal Pod Autoscaler (HPA) scales Pods based on CPU/memory metrics

Together: Docker builds and packages the app; Kubernetes runs and manages it at scale.

---

## 5. What are the best practices for containerization?

**Answer:**
- **Use minimal base images** — prefer `alpine` or distroless images to reduce attack surface and image size
- **One process per container** — containers should have a single responsibility (separation of concerns)
- **Never run as root** — use a non-root user in the Dockerfile (`USER appuser`)
- **Make images immutable** — don't modify containers at runtime; rebuild and redeploy
- **Use multi-stage builds** — separate build-time dependencies from the final runtime image to keep it lean
- **Pin image versions** — avoid `latest` tags; pin to specific digest or version to ensure reproducibility
- **Externalize configuration** — use environment variables or mounted ConfigMaps/Secrets, not hardcoded values
- **Handle SIGTERM gracefully** — ensure the app shuts down cleanly when the container is stopped
- **Use `.dockerignore`** — exclude unnecessary files (`.git`, `node_modules`, secrets) from the build context
- **Layer caching** — order Dockerfile instructions from least to most frequently changed to maximize cache reuse
- **Scan images for vulnerabilities** — use tools like Trivy, Snyk, or Docker Scout in CI
- **Set resource limits** — define CPU/memory requests and limits in Kubernetes to prevent noisy neighbors

---

## 6. How is Docker CI different from classic CI pipeline?

**Answer:**

**Classic CI pipeline:**
- Builds the app directly on the CI agent/runner machine
- Installs dependencies on the agent OS (may differ between machines)
- Output is a binary/artifact tied to the host environment
- Environment drift between dev, CI, and prod is possible

**Docker CI pipeline:**
- Builds a Docker image as the artifact (not just a binary)
- The image bundles all dependencies — the CI environment is identical to prod
- The image is pushed to a container registry and pulled at deploy time
- Any machine with Docker can run the same image identically

**Key differences:**

| | Classic CI | Docker CI |
|---|---|---|
| **Artifact** | Binary / package | Docker image |
| **Environment consistency** | Can drift | Guaranteed (image = environment) |
| **Reproducibility** | Depends on agent config | High — image is immutable |
| **Deployment** | Deploy scripts / package managers | `docker pull` + `kubectl apply` or equivalent |
| **Isolation** | Shared CI agent state | Each build produces an isolated image |
| **Portability** | Agent-specific | Run on any container host |

Docker CI effectively shifts "it works on my machine" to "the machine is defined in the image."
