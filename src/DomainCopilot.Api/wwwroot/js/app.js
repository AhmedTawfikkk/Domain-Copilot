(() => {
    "use strict";

    const state = {
        apiKey: sessionStorage.getItem("domainCopilot.apiKey") ?? "",
        role: sessionStorage.getItem("domainCopilot.role") ?? "lawyer",
        currentDocument: readSessionJson("domainCopilot.currentDocument"),
        currentMemoId: sessionStorage.getItem("domainCopilot.currentMemoId") ?? ""
    };

    const elements = {
        connectionStatus: document.querySelector("#connection-status"),
        settingsDialog: document.querySelector("#settings-dialog"),
        settingsForm: document.querySelector("#settings-form"),
        apiRole: document.querySelector("#api-role"),
        apiKey: document.querySelector("#api-key"),
        showApiKey: document.querySelector("#show-api-key"),
        clearApiKey: document.querySelector("#clear-api-key"),
        fileInput: document.querySelector("#document-file"),
        fileDropZone: document.querySelector(".file-drop-zone"),
        fileName: document.querySelector("#file-name"),
        ingestForm: document.querySelector("#ingest-form"),
        documentSource: document.querySelector("#document-source"),
        documentSummary: document.querySelector("#document-summary"),
        embeddingBatchSize: document.querySelector("#embedding-batch-size"),
        activityLog: document.querySelector("#activity-log"),
        question: document.querySelector("#question"),
        retrievalMode: document.querySelector("#retrieval-mode"),
        retrievalLimit: document.querySelector("#retrieval-limit"),
        answerForm: document.querySelector("#answer-form"),
        answerResult: document.querySelector("#answer-result"),
        evidenceResult: document.querySelector("#evidence-result"),
        reviewDocumentId: document.querySelector("#review-document-id"),
        reviewResult: document.querySelector("#review-result"),
        memoId: document.querySelector("#memo-id"),
        memoResult: document.querySelector("#memo-result"),
        memoDecisionForm: document.querySelector("#memo-decision-form"),
        counselName: document.querySelector("#counsel-name"),
        memoComment: document.querySelector("#memo-comment"),
        revisedContent: document.querySelector("#revised-content"),
        evaluationResult: document.querySelector("#evaluation-result"),
        toastRegion: document.querySelector("#toast-region")
    };

    initialise();

    function initialise() {
        elements.apiRole.value = state.role;
        renderConnectionStatus();
        renderCurrentDocument();
        if (state.currentMemoId) elements.memoId.value = state.currentMemoId;

        document.querySelectorAll(".nav-item").forEach(button => {
            button.addEventListener("click", () => showPanel(button.dataset.panel));
        });

        document.querySelector("#open-settings").addEventListener("click", () => {
            elements.apiRole.value = state.role;
            elements.apiKey.value = state.apiKey;
            elements.settingsDialog.showModal();
        });

        elements.settingsForm.addEventListener("submit", event => {
            if (event.submitter?.value !== "save") return;
            event.preventDefault();
            saveConnection();
            elements.settingsDialog.close();
        });

        elements.showApiKey.addEventListener("change", () => {
            elements.apiKey.type = elements.showApiKey.checked ? "text" : "password";
        });

        elements.clearApiKey.addEventListener("click", () => {
            state.apiKey = "";
            sessionStorage.removeItem("domainCopilot.apiKey");
            elements.apiKey.value = "";
            renderConnectionStatus();
            showToast("Connection key cleared from this tab.", "success");
        });

        elements.fileInput.addEventListener("change", () => updateSelectedFile(elements.fileInput.files[0]));
        ["dragenter", "dragover"].forEach(type => elements.fileDropZone.addEventListener(type, event => {
            event.preventDefault();
            elements.fileDropZone.classList.add("is-dragging");
        }));
        ["dragleave", "drop"].forEach(type => elements.fileDropZone.addEventListener(type, event => {
            event.preventDefault();
            elements.fileDropZone.classList.remove("is-dragging");
        }));
        elements.fileDropZone.addEventListener("drop", event => {
            const [file] = event.dataTransfer.files;
            if (!file) return;
            const transfer = new DataTransfer();
            transfer.items.add(file);
            elements.fileInput.files = transfer.files;
            updateSelectedFile(file);
        });

        elements.ingestForm.addEventListener("submit", ingestDocument);
        document.querySelector("#index-embeddings").addEventListener("click", indexPendingChunks);
        document.querySelector("#check-health").addEventListener("click", checkHealth);
        elements.answerForm.addEventListener("submit", streamAnswer);
        document.querySelector("#search-evidence").addEventListener("click", previewEvidence);
        document.querySelector("#use-current-document").addEventListener("click", useCurrentDocument);
        document.querySelector("#run-review").addEventListener("click", runLegalReview);
        document.querySelector("#load-memo").addEventListener("click", loadMemo);
        document.querySelector("#approve-memo").addEventListener("click", () => decideMemo("approve"));
        document.querySelector("#reject-memo").addEventListener("click", () => decideMemo("reject"));
        document.querySelector("#edit-approve-memo").addEventListener("click", () => decideMemo("edit-and-approve"));
        document.querySelector("#export-memo").addEventListener("click", exportMemo);
        document.querySelector("#run-evaluation").addEventListener("click", runEvaluation);

        if (!state.apiKey) elements.settingsDialog.showModal();
    }

    function showPanel(panelId) {
        document.querySelectorAll(".panel").forEach(panel => {
            const isActive = panel.id === panelId;
            panel.classList.toggle("is-active", isActive);
            panel.hidden = !isActive;
        });
        document.querySelectorAll(".nav-item").forEach(button => {
            button.classList.toggle("is-active", button.dataset.panel === panelId);
        });
        document.querySelector("main").focus?.();
    }

    function saveConnection() {
        state.apiKey = elements.apiKey.value.trim();
        state.role = elements.apiRole.value;
        sessionStorage.setItem("domainCopilot.apiKey", state.apiKey);
        sessionStorage.setItem("domainCopilot.role", state.role);
        renderConnectionStatus();
        showToast(`${capitalize(state.role)} connection saved for this tab.`, "success");
    }

    function renderConnectionStatus() {
        const label = state.apiKey ? `${capitalize(state.role)} key loaded` : "Not connected";
        elements.connectionStatus.classList.toggle("is-connected", Boolean(state.apiKey));
        elements.connectionStatus.lastElementChild.textContent = label;
    }

    async function ingestDocument(event) {
        event.preventDefault();
        const file = elements.fileInput.files[0];
        if (!file) return showToast("Choose a document before uploading.", "error");

        const formData = new FormData();
        formData.append("file", file, file.name);
        if (elements.documentSource.value.trim()) formData.append("source", elements.documentSource.value.trim());

        await runButton(event.submitter, "Uploading…", async () => {
            const { data, correlationId } = await apiFetch("/api/Ingest", { method: "POST", body: formData });
            state.currentDocument = {
                id: data.documentId,
                fileName: file.name,
                status: documentStatusLabel(data.status),
                chunkCount: data.chunkCount,
                isDuplicate: data.isDuplicate,
                correlationId
            };
            sessionStorage.setItem("domainCopilot.currentDocument", JSON.stringify(state.currentDocument));
            elements.reviewDocumentId.value = data.documentId;
            renderCurrentDocument();
            addActivity(`Uploaded ${file.name}: ${data.chunkCount} chunk(s) extracted.`, correlationId);
            showToast(data.isDuplicate ? "This document already exists in the corpus." : "Document uploaded and extracted.", "success");
        });
    }

    async function indexPendingChunks() {
        const batchSize = clampInteger(elements.embeddingBatchSize.value, 1, 100, 32);
        const button = document.querySelector("#index-embeddings");
        await runButton(button, "Indexing…", async () => {
            const { data, correlationId } = await apiFetch(`/api/Embeddings/index?batchSize=${batchSize}`, { method: "POST" });
            addActivity(`Indexed ${data.indexedChunkCount} pending chunk(s).`, correlationId);
            showToast(`${data.indexedChunkCount} chunk(s) indexed.`, "success");
        });
    }

    async function checkHealth() {
        const button = document.querySelector("#check-health");
        await runButton(button, "Checking…", async () => {
            const response = await fetch("/health/ready", { headers: { "X-Correlation-ID": crypto.randomUUID() } });
            if (!response.ok) throw new ApiError(`Readiness check returned ${response.status}.`, response.status);
            const correlationId = response.headers.get("X-Correlation-ID");
            addActivity("API readiness check passed.", correlationId);
            showToast("API and database are ready.", "success");
        });
    }

    async function streamAnswer(event) {
        event.preventDefault();
        const question = elements.question.value.trim();
        if (!question) return;
        const button = event.submitter;
        elements.answerResult.innerHTML = resultShell("Preparing grounded response…", "");
        elements.evidenceResult.innerHTML = "";

        await runButton(button, "Thinking…", async () => {
            const request = answerRequest(question);
            const result = await streamSse("/api/Answers/stream", request, delta => {
                const content = elements.answerResult.querySelector(".answer-content");
                content.textContent += delta;
            });

            if (result.type === "completed") {
                renderAnswer(result.data, false);
                addActivity("Received a grounded answer with citations.", result.correlationId);
            } else if (result.type === "refused") {
                renderAnswer(result.data, true);
                addActivity("The service refused an unsupported or ambiguous answer.", result.correlationId);
            } else {
                throw new ApiError(result.data?.message ?? "The answer stream ended unexpectedly.");
            }
        });
    }

    async function previewEvidence() {
        const question = elements.question.value.trim();
        if (!question) return showToast("Enter a question to preview evidence.", "error");
        const button = document.querySelector("#search-evidence");
        await runButton(button, "Searching…", async () => {
            const mode = encodeURIComponent(elements.retrievalMode.value);
            const limit = clampInteger(elements.retrievalLimit.value, 1, 20, 5);
            const { data, correlationId } = await apiFetch(`/api/Retrieval?query=${encodeURIComponent(question)}&mode=${mode}&limit=${limit}`);
            renderEvidence(data);
            addActivity(`Previewed ${data.length} retrieved evidence chunk(s).`, correlationId);
        });
    }

    function useCurrentDocument() {
        if (!state.currentDocument?.id) return showToast("Upload a document first, or paste a document ID.", "error");
        elements.reviewDocumentId.value = state.currentDocument.id;
    }

    async function runLegalReview() {
        const documentId = elements.reviewDocumentId.value.trim();
        if (!isGuid(documentId)) return showToast("Enter a valid document ID.", "error");
        const button = document.querySelector("#run-review");
        elements.reviewResult.innerHTML = resultShell("The legal review agents are working. This may take a moment…", "");
        await runButton(button, "Reviewing…", async () => {
            const { data, correlationId } = await apiFetch("/api/LegalReviews", {
                method: "POST",
                json: { documentId }
            });
            renderReview(data);
            if (data.memoDraft?.memoId) {
                state.currentMemoId = data.memoDraft.memoId;
                sessionStorage.setItem("domainCopilot.currentMemoId", state.currentMemoId);
                elements.memoId.value = state.currentMemoId;
            }
            addActivity(`Legal review completed for ${data.fileName ?? documentId}.`, correlationId);
        });
    }

    async function loadMemo() {
        const memoId = elements.memoId.value.trim();
        if (!isGuid(memoId)) return showToast("Enter a valid memo ID.", "error");
        const button = document.querySelector("#load-memo");
        await runButton(button, "Loading…", async () => {
            const { data, correlationId } = await apiFetch(`/api/ReviewMemos/${memoId}`);
            state.currentMemoId = memoId;
            sessionStorage.setItem("domainCopilot.currentMemoId", memoId);
            renderMemo(data);
            addActivity("Loaded review memo.", correlationId);
        });
    }

    async function decideMemo(action) {
        const memoId = elements.memoId.value.trim();
        const counselName = elements.counselName.value.trim();
        const comment = elements.memoComment.value.trim();
        if (!isGuid(memoId)) return showToast("Load a valid memo first.", "error");
        if (!counselName) return showToast("Enter the counsel name.", "error");
        if (action === "reject" && !comment) return showToast("A rejection comment is required.", "error");
        if (action === "edit-and-approve" && !elements.revisedContent.value.trim()) return showToast("Enter revised memo content before approving.", "error");

        const button = document.querySelector(`#${action === "edit-and-approve" ? "edit-approve" : action}-memo`);
        const payload = action === "edit-and-approve"
            ? { counselName, revisedContent: elements.revisedContent.value.trim(), comment: comment || null }
            : { counselName, comment: comment || null };
        await runButton(button, "Saving…", async () => {
            const { data, correlationId } = await apiFetch(`/api/ReviewMemos/${memoId}/${action}`, { method: "POST", json: payload });
            renderMemo(data);
            addActivity(`Memo ${action.replaceAll("-", " ")} by ${counselName}.`, correlationId);
            showToast("Memo decision saved.", "success");
        });
    }

    async function exportMemo() {
        const memoId = elements.memoId.value.trim();
        if (!isGuid(memoId)) return showToast("Load a valid memo first.", "error");
        const button = document.querySelector("#export-memo");
        await runButton(button, "Preparing…", async () => {
            const response = await apiFetchRaw(`/api/ReviewMemos/${memoId}/export/docx`);
            if (!response.ok) throw await toApiError(response);
            const fileName = readFileName(response.headers.get("content-disposition")) ?? "review-memo.docx";
            const blob = await response.blob();
            const url = URL.createObjectURL(blob);
            const link = document.createElement("a");
            link.href = url; link.download = fileName; link.click(); URL.revokeObjectURL(url);
            addActivity(`Downloaded ${fileName}.`, response.headers.get("X-Correlation-ID"));
            showToast("Approved memo downloaded.", "success");
        });
    }

    async function runEvaluation() {
        const button = document.querySelector("#run-evaluation");
        elements.evaluationResult.innerHTML = resultShell("Running the golden evaluation set. Keep this tab open…", "");
        await runButton(button, "Running…", async () => {
            const { data, correlationId } = await apiFetch("/api/Evaluation/run", { method: "POST" });
            renderEvaluation(data);
            addActivity("Golden evaluation run completed.", correlationId);
        });
    }

    function answerRequest(question) {
        return {
            question,
            retrievalMode: Number(elements.retrievalMode.value),
            retrievalLimit: clampInteger(elements.retrievalLimit.value, 1, 20, 5)
        };
    }

    async function streamSse(path, body, onDelta) {
        const response = await apiFetchRaw(path, { method: "POST", json: body });
        const correlationId = response.headers.get("X-Correlation-ID");
        if (!response.ok) throw await toApiError(response);
        if (!response.body) throw new ApiError("Streaming is unavailable in this browser.");

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";
        let eventName = "";

        while (true) {
            const { done, value } = await reader.read();
            buffer += decoder.decode(value ?? new Uint8Array(), { stream: !done });
            const events = buffer.split("\n\n");
            buffer = events.pop() ?? "";
            for (const rawEvent of events) {
                const parsed = parseSseEvent(rawEvent);
                if (!parsed) continue;
                eventName = parsed.event;
                const data = safeJson(parsed.data);
                if (eventName === "delta") onDelta(data?.delta ?? "");
                if (["completed", "refused", "error"].includes(eventName)) return { type: eventName, data, correlationId };
            }
            if (done) break;
        }
        return { type: eventName || "error", data: null, correlationId };
    }

    async function apiFetch(path, options = {}) {
        const response = await apiFetchRaw(path, options);
        if (!response.ok) throw await toApiError(response);
        return { data: await response.json(), correlationId: response.headers.get("X-Correlation-ID") };
    }

    async function apiFetchRaw(path, { method = "GET", json, body } = {}) {
        if (!state.apiKey) {
            elements.settingsDialog.showModal();
            throw new ApiError("Add an API key in Connection settings first.", 401);
        }
        const headers = { "X-Api-Key": state.apiKey, "X-Correlation-ID": crypto.randomUUID() };
        if (json !== undefined) headers["Content-Type"] = "application/json";
        return fetch(path, { method, headers, body: json !== undefined ? JSON.stringify(json) : body });
    }

    async function toApiError(response) {
        const text = await response.text();
        const content = safeJson(text);
        const message = content?.error ?? content?.title ?? content?.message ?? content?.terminationMessage ?? text ?? `Request failed (${response.status}).`;
        return new ApiError(message, response.status);
    }

    function renderCurrentDocument() {
        if (!state.currentDocument) return;
        const document = state.currentDocument;
        elements.documentSummary.innerHTML = `
            <div class="document-grid">
                ${metadata("File", document.fileName)}
                ${metadata("Document ID", document.id)}
                ${metadata("Chunks", document.chunkCount ?? "—")}
                ${metadata("Status", document.status ?? "Ingested")}
                ${metadata("Request trace", document.correlationId ?? "—")}
                ${metadata("Duplicate", document.isDuplicate ? "Yes" : "No")}
            </div>`;
    }

    function renderAnswer(result, refused) {
        const citations = result.citations ?? [];
        elements.answerResult.innerHTML = `
            <article class="result-card ${refused ? "is-refused" : "is-success"}">
                <span class="status-pill ${refused ? "warn" : ""}">${refused ? "Refused safely" : "Grounded answer"}</span>
                <h2>${refused ? "The service needs stronger evidence" : "Answer"}</h2>
                <p class="answer-content">${escapeHtml(result.answer || result.refusalReason || "No answer was returned.")}</p>
                ${refused ? `<p class="hint"><strong>Reason:</strong> ${escapeHtml(result.refusalReason || "Insufficient evidence.")}</p>` : renderCitations(citations)}
            </article>`;
    }

    function renderEvidence(chunks) {
        if (!chunks.length) {
            elements.evidenceResult.innerHTML = resultShell("No evidence chunks matched this question.", "is-refused");
            return;
        }
        elements.evidenceResult.innerHTML = `<article class="result-card"><h2>Retrieved evidence</h2><p class="hint">This is the evidence available to the grounded-answer service.</p><ul class="chunk-list">${chunks.map(chunk => `
            <li class="chunk"><strong>${escapeHtml(chunk.fileName)}</strong><span class="risk-badge">Score ${Number(chunk.score).toFixed(3)}</span><p>${escapeHtml(chunk.clauseOrSection || "Unclassified section")} · page ${chunk.pageNumber ?? "—"}</p><p>${escapeHtml(chunk.content)}</p></li>`).join("")}</ul></article>`;
    }

    function renderReview(result) {
        if (Number(result.status) !== 0) {
            elements.reviewResult.innerHTML = `<article class="result-card is-refused"><span class="status-pill warn">Review terminated safely</span><h2>Review not completed</h2><p>${escapeHtml(result.terminationMessage || "The workflow could not produce a reliable review.")}</p></article>`;
            return;
        }
        const findings = result.riskFindings ?? [];
        const memo = result.memoDraft;
        elements.reviewResult.innerHTML = `<article class="result-card is-success"><span class="status-pill">Review complete</span><h2>${escapeHtml(result.fileName || "Legal review")}</h2><p class="hint">${result.extractedClauses?.length ?? 0} clause(s) extracted · ${findings.length} risk finding(s)</p>${renderFindings(findings)}${memo ? `<hr><h3>Memo draft</h3><p class="memo-content">${escapeHtml(memo.content)}</p><p class="hint">Memo ID: <code>${memo.memoId}</code></p><button class="secondary-button" id="open-memo-from-review" type="button">Open in approval desk</button>` : ""}</article>`;
        document.querySelector("#open-memo-from-review")?.addEventListener("click", () => {
            elements.memoId.value = memo.memoId;
            showPanel("approval-panel");
            loadMemo();
        });
    }

    function renderMemo(memo) {
        elements.memoDecisionForm.hidden = false;
        elements.revisedContent.value = memo.content ?? "";
        const status = approvalStatusLabel(memo.approvalStatus);
        elements.memoResult.innerHTML = `<article class="result-card"><span class="status-pill ${status === "Rejected" ? "error" : status === "Draft" ? "warn" : ""}">${escapeHtml(status)}</span><h2>Review memo</h2><p class="memo-content">${escapeHtml(memo.content || "")}</p><div class="document-grid">${metadata("Created", formatDate(memo.createdAtUtc))}${metadata("Decided", memo.decidedAtUtc ? formatDate(memo.decidedAtUtc) : "Pending")}${metadata("Decision by", memo.decidedBy || "—")}</div>${memo.decisionComment ? `<p class="hint"><strong>Comment:</strong> ${escapeHtml(memo.decisionComment)}</p>` : ""}</article>`;
    }

    function renderEvaluation(result) {
        const cases = result.cases ?? [];
        const passed = cases.filter(item =>
            item.outcomeMatched &&
            (item.grounded ?? true) &&
            (item.citationMatched ?? true)).length;
        elements.evaluationResult.innerHTML = `<article class="result-card ${passed === cases.length ? "is-success" : "is-refused"}"><h2>Evaluation result</h2><p><strong>${passed} / ${cases.length}</strong> case(s) passed all applicable expected outcome, grounding, and citation checks.</p><ul class="finding-list">${cases.map(item => { const casePassed = item.outcomeMatched && (item.grounded ?? true) && (item.citationMatched ?? true); return `<li class="finding"><strong>${escapeHtml(item.id)} · ${escapeHtml(evaluationScenarioLabel(item.scenario))}</strong><span class="status-pill ${casePassed ? "" : "warn"}">${casePassed ? "Passed" : "Needs review"}</span><p>Outcome: ${item.outcomeMatched ? "matched" : "did not match"} · Grounded: ${item.grounded == null ? "not applicable" : item.grounded ? "yes" : "no"} · Citations: ${item.citationMatched == null ? "not applicable" : item.citationMatched ? "matched" : "did not match"}</p></li>`; }).join("")}</ul></article>`;
    }

    function renderCitations(citations) {
        if (!citations.length) return "<p class=\"hint\">No citations were returned.</p>";
        return `<h3>Citations</h3><ul class="citation-list">${citations.map(citation => `<li class="citation"><strong>${escapeHtml(citation.fileName)}</strong><span>${escapeHtml(citation.clauseOrSection || "Unclassified section")} · page ${citation.pageNumber ?? "—"}${citation.lowConfidence ? " · low extraction confidence" : ""}</span></li>`).join("")}</ul>`;
    }

    function renderFindings(findings) {
        if (!findings.length) return "<p class=\"hint\">No risk findings were produced for the extracted clauses.</p>";
        return `<h3>Risk findings</h3><ul class="finding-list">${findings.map(finding => `<li class="finding"><strong>${escapeHtml(finding.title)}</strong><span class="risk-badge ${riskSeverityLabel(finding.severity).toLowerCase()}">${escapeHtml(riskSeverityLabel(finding.severity))}</span><p>${escapeHtml(finding.rationale)}</p><p><strong>Recommendation:</strong> ${escapeHtml(finding.recommendation)}</p></li>`).join("")}</ul>`;
    }

    function resultShell(message, modifier) {
        return `<article class="result-card ${modifier}"><p class="streaming-indicator">${escapeHtml(message)}</p><p class="answer-content"></p></article>`;
    }

    function addActivity(message, correlationId) {
        const entry = document.createElement("li");
        entry.textContent = `${new Date().toLocaleTimeString()} — ${message}${correlationId ? ` Trace: ${correlationId}` : ""}`;
        elements.activityLog.prepend(entry);
    }

    async function runButton(button, busyLabel, operation) {
        const originalLabel = button.textContent;
        button.disabled = true;
        button.textContent = busyLabel;
        try {
            await operation();
        } catch (error) {
            console.error(error);
            const message = error instanceof ApiError ? error.message : "An unexpected error occurred.";
            showToast(message, "error");
            addActivity(`Request failed: ${message}`);
        } finally {
            button.disabled = false;
            button.textContent = originalLabel;
        }
    }

    function parseSseEvent(raw) {
        const lines = raw.replace(/\r/g, "").split("\n");
        const event = lines.find(line => line.startsWith("event:"))?.slice(6).trim();
        const data = lines.filter(line => line.startsWith("data:")).map(line => line.slice(5).trim()).join("\n");
        return event ? { event, data } : null;
    }

    function updateSelectedFile(file) { elements.fileName.textContent = file ? file.name : "Choose a contract file"; }
    function metadata(label, value) { return `<div><span class="metadata-label">${escapeHtml(label)}</span><span class="metadata-value">${escapeHtml(String(value))}</span></div>`; }
    function safeJson(value) { try { return JSON.parse(value); } catch { return null; } }
    function readSessionJson(key) { const value = sessionStorage.getItem(key); return value ? safeJson(value) : null; }
    function escapeHtml(value) { const node = document.createElement("span"); node.textContent = value ?? ""; return node.innerHTML; }
    function capitalize(value) { return String(value).charAt(0).toUpperCase() + String(value).slice(1); }
    function isGuid(value) { return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value); }
    function clampInteger(value, min, max, fallback) { const number = Number.parseInt(value, 10); return Number.isFinite(number) ? Math.min(max, Math.max(min, number)) : fallback; }
    function enumLabel(value) { return String(value ?? "Unknown").replace(/([a-z])([A-Z])/g, "$1 $2"); }
    function approvalStatusLabel(value) { return ["Draft", "Approved", "Rejected"][Number(value)] ?? enumLabel(value); }
    function documentStatusLabel(value) { return ["Pending", "Processing", "Chunked", "Indexed", "Failed"][Number(value)] ?? enumLabel(value); }
    function riskSeverityLabel(value) { return ["Low", "Medium", "High", "Critical"][Number(value)] ?? enumLabel(value); }
    function evaluationScenarioLabel(value) { return ["Baseline", "Out of corpus", "Ambiguous", "Direct prompt injection", "Indirect prompt injection", "Conflicting sources"][Number(value)] ?? enumLabel(value); }
    function formatDate(value) { return new Date(value).toLocaleString(); }
    function readFileName(value) { const match = /filename\*?=(?:UTF-8''|\")?([^;\"]+)/i.exec(value ?? ""); return match ? decodeURIComponent(match[1].replaceAll("\"", "")) : null; }
    function showToast(message, kind = "") { const toast = document.querySelector("#toast-template").content.firstElementChild.cloneNode(true); toast.textContent = message; toast.classList.toggle("is-error", kind === "error"); toast.classList.toggle("is-success", kind === "success"); elements.toastRegion.append(toast); setTimeout(() => toast.remove(), 5000); }

    class ApiError extends Error { constructor(message, status) { super(message); this.name = "ApiError"; this.status = status; } }
})();
