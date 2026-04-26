# Chatbot Setup

Setup navodila za Chatbot izdelan v .NET

## Prerequisites

Potrebno je instalirati naslednje:

| Tool | Windows | Linux | macOS |
|------|---------|-------|-------|
| .NET 9 SDK | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| Node.js + npx | [nodejs.org](https://nodejs.org) | `sudo apt install nodejs npm` | `brew install node` |
| Ollama (opcijsko) | [ollama.com](https://ollama.com) | [ollama.com](https://ollama.com) | `brew install ollama` |

---

## Konfiguracija

Potrebno je nastaviti dva konfiguracijska fajla

- **`appsettings.json`** — LLM provider, model
- **User secrets** — API kljuc za LLM, ce se uporablja komercijalni provider (ChatGPT, Claude)

---

### 1. `appsettings.json`

Datoteka je v `MCPClient` project direktoriju in je naslednje oblike:

```json
{
    "McpServer": {
        "ProjectPath": "Path _do_MCPServer"
    },
    "McpServerEverything": {
        "Command": "npx",
        "Arguments": "-y @modelcontextprotocol/server-everything"
    },
    "Ollama": {
        "BaseUrl": "http://localhost:11434" // Potrebno samo ce se uporablja Ollama
    },
    "Llm": {
        "Provider": "ollama",
        "Model": "qwen2.5:7b"
    }
}
```

#### `McpServer:ProjectPath`

Odvisno od operativnega sistema je potrebno nastaviti ustrezen path.

**Windows:**
```
"ProjectPath": "C:/Users/YourName/path/to/MCPServer"
```

**Linux / macOS:**
```
"ProjectPath": "/home/yourname/path/to/MCPServer"
```

#### `Llm:Provider` in `Llm:Model`

Nastavi na `ollama`, `claude`, ali `openai` in izberi ustrezen model:

#### `McpServerEverything:Command`

Moglo bi biti enako na Linux/MacOs/Windows

### 2. User Secrets (samo za API kljuc)

Potrebno ce se uporablja OpenAI/Claude model

```bash
dotnet user-secrets set "Llm:ApiKey" "vas-api-kljuc"
```

Ollama deluje lokalno in ne potrebuje kljuc.

---

## Testiranje

Iz root direktorija pozenite ukaz:

```bash
dotnet test
```

Poleg testa za celoten pipeline je tudi test ki preverja Claude pipeline z "mock" kljucem tako da preveri ce api vrne "Unauthoritzed" napako.

## Uporabljeni paketi

### 1. ModelContextProtocol
Uradna knjiznica za delo s MCP serverji v .NET

### 2. Microsoft.SemanticKernel
Uporablja se da poenoti implementacijo za razlicne LLM providerje. Avtomatsko hadluje registracijo toolov, zgodovino razgovora, streaming, ipd. Lahko bi se uporabil tudi LangChain, vendar je SemanticKernel narejen specificno za c#.

### 3. Anthropic.SDK
Ker SemanticKernel ne podpira Anthropic po defaultu, se uporablja Anthropic.SDK ce je izbrani provider "claude".

### NPX
Npx je potreben za javni MCP streznik: @modelcontextprotocol/server-everything

