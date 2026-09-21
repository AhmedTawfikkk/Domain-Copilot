# Web UI

The API hosts a minimal, dependency-free browser interface from
`src/DomainCopilot.Api/wwwroot`. It is deliberately a plain HTML, CSS, and
JavaScript client rather than a separate SPA: the product requirement is a
functional surface for the existing API, with no additional Node build chain,
CORS configuration, or duplicate authentication implementation.

Open the API root URL after starting the service. For the development HTTPS
profile this is normally `https://localhost:7082/`; IIS Express may use a
different local port.

## Available workflows

- **Workspace**: upload PDF, DOCX, or TXT documents and index pending chunks.
- **Ask documents**: stream a grounded answer over SSE, show a safe refusal, or
  preview the retrieved evidence before asking.
- **Legal review**: run the existing clause extraction, risk assessment, and
  memo drafting pipeline for a document ID.
- **Approval desk**: retrieve, approve, reject, edit-and-approve, and export a
  review memo.
- **Evaluation**: run the counsel-only golden evaluation set and present its
  case-level results.

## Authentication and security

The UI asks for either a Lawyer or Counsel API key in its connection dialog and
sends it only as the existing `X-Api-Key` request header. It retains the key in
browser `sessionStorage`, never local storage, a URL, a log, or a source file.
Closing the tab clears it. This is appropriate for the local, demonstrative UI;
the server remains the authority for every role and endpoint.

Never commit a real key. Configure keys through `.env` or deployment secrets as
documented by `.env.example`.

## Verification

```powershell
dotnet build DomainCopilot.sln --configuration Release --no-restore
dotnet test DomainCopilot.sln --configuration Release --no-build --no-restore
```

Then start the API with valid OCR executable paths and open its root URL. The
browser should show the connection dialog before it permits an API request.
