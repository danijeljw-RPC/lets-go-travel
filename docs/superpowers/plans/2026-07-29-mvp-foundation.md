# MVP Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the buildable, testable .NET 10 application foundation for the `readytogo.travel` API, Blazor SSR web app, general worker and dedicated flight-reconciliation worker.

**Architecture:** Use the feature-oriented modular-monolith boundary accepted by ADR-0009. This first slice provides only hosting and public-contract primitives: `/api/v1`, `ProblemDetails`, correlation IDs, health checks, OpenAPI, four deployables, container definitions and architecture tests. Product modules and external services land in later vertical slices.

**Tech Stack:** .NET SDK 10.0.300, ASP.NET Core 10, Blazor Web App static/interactive server rendering, Worker Service, xUnit, built-in OpenAPI, built-in health checks, built-in rate limiting, Docker and GitHub Actions.

## Global Constraints

- Product name is `readytogo.travel`; shortcode is `RTGT`; root namespace is `ReadyToGoTravel`.
- Target framework is exactly `net10.0`; nullable and implicit usings are enabled; warnings are errors in repository code.
- Public API routes begin with `/api/v1` and errors use `ProblemDetails` with stable `code` and `correlationId` extensions.
- The Blazor web app must not bypass the public API for product behavior.
- No supplier, payment, identity, database or production secret is required by this slice.
- No C360, tenant, TMC, reseller or enterprise product assumptions may enter the codebase.
- Production supplier/payment capabilities remain disabled until their review-register gates are approved.
- Tests are written before the implementation behavior they prove.

---

### Task 1: Solution and build conventions

**Files:**
- Create: `ReadyToGoTravel.slnx`
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.editorconfig`
- Modify: `.gitignore`
- Create: `src/ReadyToGoTravel.BuildingBlocks/ReadyToGoTravel.BuildingBlocks.csproj`
- Create: `tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj`
- Test: `tests/ReadyToGoTravel.Architecture.Tests/SolutionConventionsTests.cs`

**Interfaces:**
- Consumes: .NET 10 SDK.
- Produces: repository-wide build settings and a solution that later tasks extend.

- [ ] **Step 1: Create the solution and projects**

```bash
dotnet new sln --format slnx --name ReadyToGoTravel
dotnet new classlib --framework net10.0 --name ReadyToGoTravel.BuildingBlocks --output src/ReadyToGoTravel.BuildingBlocks
dotnet new xunit --framework net10.0 --name ReadyToGoTravel.Architecture.Tests --output tests/ReadyToGoTravel.Architecture.Tests
dotnet sln ReadyToGoTravel.slnx add src/ReadyToGoTravel.BuildingBlocks/ReadyToGoTravel.BuildingBlocks.csproj tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj
```

- [ ] **Step 2: Write failing convention tests**

```csharp
[Fact]
public void AllProjectsTargetNet10()
{
    var projects = RepositoryFiles.ProjectFiles();
    Assert.All(projects, project => Assert.Contains("<TargetFramework>net10.0</TargetFramework>", File.ReadAllText(project)));
}

[Fact]
public void ProjectNamesUseReadyToGoTravelPrefix()
{
    Assert.All(RepositoryFiles.ProjectFiles(), project =>
        Assert.StartsWith("ReadyToGoTravel.", Path.GetFileNameWithoutExtension(project)));
}
```

- [ ] **Step 3: Run the tests and confirm the convention failure**

Run: `dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj`

Expected: FAIL until the repository root resolver and exact convention files are implemented.

- [ ] **Step 4: Add exact build conventions**

`global.json` pins SDK `10.0.300` with `rollForward: latestPatch`. `Directory.Build.props` sets `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, deterministic builds and generated documentation only where explicitly enabled. `Directory.Packages.props` enables central package management and pins all test/OpenAPI packages.

- [ ] **Step 5: Run the architecture tests**

Run: `dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add .editorconfig .gitignore Directory.Build.props Directory.Packages.props ReadyToGoTravel.slnx global.json src/ReadyToGoTravel.BuildingBlocks tests/ReadyToGoTravel.Architecture.Tests
git commit -m "build: create dotnet solution foundation"
```

### Task 2: API contract foundation

**Files:**
- Create: `src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj`
- Create: `src/ReadyToGoTravel.Api/Program.cs`
- Create: `src/ReadyToGoTravel.Api/Infrastructure/CorrelationIdMiddleware.cs`
- Create: `src/ReadyToGoTravel.Api/Infrastructure/PlatformProblemDetails.cs`
- Create: `src/ReadyToGoTravel.Api/Endpoints/PlatformEndpoints.cs`
- Create: `src/ReadyToGoTravel.Api/appsettings.json`
- Create: `src/ReadyToGoTravel.Api/appsettings.Development.json`
- Test: `tests/ReadyToGoTravel.Api.Tests/ReadyToGoTravel.Api.Tests.csproj`
- Test: `tests/ReadyToGoTravel.Api.Tests/ApiContractTests.cs`

**Interfaces:**
- Consumes: ASP.NET Core hosting and the shared build conventions.
- Produces: `GET /api/v1/platform`, `GET /health/live`, `GET /health/ready`, development OpenAPI and the `X-Correlation-ID` response contract.

- [ ] **Step 1: Scaffold the API and integration-test project**

```bash
dotnet new webapi --framework net10.0 --use-controllers false --no-https --name ReadyToGoTravel.Api --output src/ReadyToGoTravel.Api
dotnet new xunit --framework net10.0 --name ReadyToGoTravel.Api.Tests --output tests/ReadyToGoTravel.Api.Tests
dotnet add tests/ReadyToGoTravel.Api.Tests/ReadyToGoTravel.Api.Tests.csproj reference src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj
dotnet sln ReadyToGoTravel.slnx add src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj tests/ReadyToGoTravel.Api.Tests/ReadyToGoTravel.Api.Tests.csproj
```

- [ ] **Step 2: Write failing API contract tests**

```csharp
[Fact]
public async Task PlatformEndpointUsesV1AndReturnsProductIdentity()
{
    using var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/v1/platform");
    response.EnsureSuccessStatusCode();
    var payload = await response.Content.ReadFromJsonAsync<PlatformResponse>();
    Assert.Equal("readytogo.travel", payload!.Product);
    Assert.Equal("v1", payload.ApiVersion);
}

[Fact]
public async Task CorrelationIdIsEchoed()
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
    request.Headers.Add("X-Correlation-ID", "test-correlation");
    var response = await _factory.CreateClient().SendAsync(request);
    Assert.Equal("test-correlation", response.Headers.GetValues("X-Correlation-ID").Single());
}
```

- [ ] **Step 3: Run the tests and confirm failure**

Run: `dotnet test tests/ReadyToGoTravel.Api.Tests/ReadyToGoTravel.Api.Tests.csproj`

Expected: FAIL because the platform endpoint and correlation middleware do not exist.

- [ ] **Step 4: Implement the minimal API contract**

```csharp
var v1 = app.MapGroup("/api/v1");
v1.MapGet("/platform", () => TypedResults.Ok(new PlatformResponse("readytogo.travel", "RTGT", "v1")));

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
```

Correlation middleware validates a printable identifier up to 128 characters, otherwise creates a lowercase UUIDv7 string. It adds the value to the response header, trace state and `HttpContext.TraceIdentifier`.

Unknown API routes and framework failures return `application/problem+json`; the customization callback adds `code` and `correlationId` without exposing exception details outside development.

- [ ] **Step 5: Apply built-in rate limits and OpenAPI**

The public API has a global safety limiter plus named policies for future search and command groups. `AddOpenApi` and `MapOpenApi` run only in development; CI generates the document through the supported .NET build path.

- [ ] **Step 6: Run API and full tests**

Run: `dotnet test ReadyToGoTravel.slnx`

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add ReadyToGoTravel.slnx Directory.Packages.props src/ReadyToGoTravel.Api tests/ReadyToGoTravel.Api.Tests
git commit -m "feat: add versioned api contract foundation"
```

### Task 3: Blazor SSR web host

**Files:**
- Create: `src/ReadyToGoTravel.Web/ReadyToGoTravel.Web.csproj`
- Create: `src/ReadyToGoTravel.Web/Program.cs`
- Create: `src/ReadyToGoTravel.Web/Components/App.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Routes.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Layout/MainLayout.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Home.razor`
- Create: `src/ReadyToGoTravel.Web/Client/PlatformApiClient.cs`
- Create: `src/ReadyToGoTravel.Web/wwwroot/app.css`
- Test: `tests/ReadyToGoTravel.Architecture.Tests/WebBoundaryTests.cs`

**Interfaces:**
- Consumes: `GET /api/v1/platform` through `PlatformApiClient`.
- Produces: a Blazor SSR host with no direct product-domain project dependency.

- [ ] **Step 1: Scaffold the Blazor Web App**

```bash
dotnet new blazor --framework net10.0 --interactivity Server --empty --name ReadyToGoTravel.Web --output src/ReadyToGoTravel.Web
dotnet sln ReadyToGoTravel.slnx add src/ReadyToGoTravel.Web/ReadyToGoTravel.Web.csproj
```

- [ ] **Step 2: Write the failing web-boundary test**

```csharp
[Fact]
public void WebProjectReferencesNoProductModuleAssembly()
{
    var references = ProjectXml.References("src/ReadyToGoTravel.Web/ReadyToGoTravel.Web.csproj");
    Assert.DoesNotContain(references, value => value.Contains("Modules.", StringComparison.Ordinal));
}
```

- [ ] **Step 3: Run the architecture test and confirm the missing convention support**

Run: `dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj`

Expected: FAIL until project-reference parsing and the new web project are included.

- [ ] **Step 4: Implement the SSR host and API client boundary**

Use static SSR for the shell. Register a named `HttpClient` whose base URL comes from `PlatformApi:BaseUrl`. The home page renders product identity and readiness returned by the API, with an unavailable state on API failure. No product repository or module is injected into the web project.

- [ ] **Step 5: Run tests and build**

Run: `dotnet test ReadyToGoTravel.slnx && dotnet build ReadyToGoTravel.slnx --no-restore`

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add ReadyToGoTravel.slnx src/ReadyToGoTravel.Web tests/ReadyToGoTravel.Architecture.Tests
git commit -m "feat: add blazor ssr web host"
```

### Task 4: Worker hosting and cancellation boundary

**Files:**
- Create: `src/ReadyToGoTravel.Worker/ReadyToGoTravel.Worker.csproj`
- Create: `src/ReadyToGoTravel.Worker/Program.cs`
- Create: `src/ReadyToGoTravel.Worker/Worker.cs`
- Create: `src/ReadyToGoTravel.FlightReconciliation.Worker/ReadyToGoTravel.FlightReconciliation.Worker.csproj`
- Create: `src/ReadyToGoTravel.FlightReconciliation.Worker/Program.cs`
- Create: `src/ReadyToGoTravel.FlightReconciliation.Worker/Worker.cs`
- Create: `src/ReadyToGoTravel.BuildingBlocks/Hosting/CooperativeWorker.cs`
- Test: `tests/ReadyToGoTravel.BuildingBlocks.Tests/ReadyToGoTravel.BuildingBlocks.Tests.csproj`
- Test: `tests/ReadyToGoTravel.BuildingBlocks.Tests/CooperativeWorkerTests.cs`

**Interfaces:**
- Consumes: `TimeProvider` and `CancellationToken`.
- Produces: abstract `CooperativeWorker.ExecuteCycleAsync(CancellationToken)` used by both worker hosts; no public ingress.

- [ ] **Step 1: Write the failing cancellation test**

```csharp
[Fact]
public async Task WorkerStopsPromptlyWhenCancellationIsRequested()
{
    var worker = new RecordingWorker(TimeProvider.System, TimeSpan.FromHours(1));
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
    await worker.RunForTestAsync(cancellation.Token);
    Assert.True(worker.CycleCount >= 1);
}
```

- [ ] **Step 2: Run the focused test and confirm failure**

Run: `dotnet test tests/ReadyToGoTravel.BuildingBlocks.Tests/ReadyToGoTravel.BuildingBlocks.Tests.csproj`

Expected: FAIL because `CooperativeWorker` does not exist.

- [ ] **Step 3: Implement cooperative worker base and hosts**

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await ExecuteCycleAsync(stoppingToken);
        await Task.Delay(_idleDelay, _timeProvider, stoppingToken);
    }
}
```

The first cycle performs no domain work and logs one structured readiness event. Later slices replace `ExecuteCycleAsync` through injected services. Both hosts use health-check publishing and validate configuration at startup.

- [ ] **Step 4: Run all tests and build**

Run: `dotnet test ReadyToGoTravel.slnx && dotnet build ReadyToGoTravel.slnx --no-restore`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add ReadyToGoTravel.slnx src/ReadyToGoTravel.BuildingBlocks src/ReadyToGoTravel.Worker src/ReadyToGoTravel.FlightReconciliation.Worker tests/ReadyToGoTravel.BuildingBlocks.Tests
git commit -m "feat: add cooperative worker hosts"
```

### Task 5: Containers, CI and foundation verification

**Files:**
- Create: `src/ReadyToGoTravel.Api/Dockerfile`
- Create: `src/ReadyToGoTravel.Web/Dockerfile`
- Create: `src/ReadyToGoTravel.Worker/Dockerfile`
- Create: `src/ReadyToGoTravel.FlightReconciliation.Worker/Dockerfile`
- Create: `.dockerignore`
- Create: `.github/workflows/ci.yml`
- Modify: `README.md`
- Modify: `docs/plans/active/PLAN-0001-project-planning-readiness.md`
- Create: `docs/plans/active/PLAN-0002-mvp-delivery.md`

**Interfaces:**
- Consumes: the complete Slice 1 solution.
- Produces: deterministic SDK build/test workflow and four non-root runtime images.

- [ ] **Step 1: Add multi-stage non-root Dockerfiles**

Each Dockerfile restores the solution/project using `mcr.microsoft.com/dotnet/sdk:10.0`, publishes the exact deployable and runs on `mcr.microsoft.com/dotnet/aspnet:10.0` (web/API) or `mcr.microsoft.com/dotnet/runtime:10.0` (workers) as the image-provided non-root user. No secret is copied into an image.

- [ ] **Step 2: Add CI**

```yaml
- uses: actions/setup-dotnet@v5
  with:
    dotnet-version: 10.0.x
- run: dotnet restore ReadyToGoTravel.slnx --locked-mode
- run: dotnet build ReadyToGoTravel.slnx --no-restore --configuration Release
- run: dotnet test ReadyToGoTravel.slnx --no-build --configuration Release --collect:"XPlat Code Coverage"
```

CI also runs `dotnet format --verify-no-changes`, checks documentation links/structure with the repository validator and builds all four Dockerfiles.

- [ ] **Step 3: Update project navigation and plan state**

Document the four deployables, local build/test commands and production-gate rule. PLAN-0001 records implementation readiness; PLAN-0002 sequences the remaining six MVP slices.

- [ ] **Step 4: Run fresh verification**

Run:

```bash
dotnet restore ReadyToGoTravel.slnx
dotnet format ReadyToGoTravel.slnx --verify-no-changes --no-restore
dotnet build ReadyToGoTravel.slnx --no-restore --configuration Release
dotnet test ReadyToGoTravel.slnx --no-build --configuration Release
docker build -f src/ReadyToGoTravel.Api/Dockerfile .
docker build -f src/ReadyToGoTravel.Web/Dockerfile .
docker build -f src/ReadyToGoTravel.Worker/Dockerfile .
docker build -f src/ReadyToGoTravel.FlightReconciliation.Worker/Dockerfile .
```

Expected: every command exits zero; no production secret or supplier dependency is required.

- [ ] **Step 5: Commit**

```bash
git add .dockerignore .github README.md docs/plans src/*/Dockerfile
git commit -m "ci: verify mvp foundation containers"
```

## Plan Self-review

- Spec coverage: Slice 1 deployables, API contract, SSR boundary, workers, containers and CI each have a task.
- Placeholder scan: no `TBD`, `TODO`, `implement later` or unspecified error/test step remains.
- Type consistency: project names, route names, `PlatformResponse`, correlation header and `CooperativeWorker` are consistent across tasks.
- Scope: no database, Keycloak, supplier, payment or product module is pulled into the foundation slice.
