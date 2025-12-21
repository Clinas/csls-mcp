PROMPT MAESTRO — C# Language Service MCP para Gemini CLI
Quiero que desarrolles una herramienta COMPLETA llamada `csharp-mcp-server`.

🎯 OBJETIVO
Crear un **MCP Server (Model Context Protocol)** que provea un **C# Language Service** para agentes de IA, usando **Roslyn**, y que se integre de forma nativa con **Gemini CLI** SIN modificar el código fuente de Gemini.

La herramienta debe permitir a un agente:

- Resolver símbolos C# (clases, interfaces, namespaces, métodos)
- Encontrar definiciones y referencias
- Listar miembros de tipos
- Obtener código fuente exacto
  Todo de forma semántica (NO por búsqueda de texto).

---

## 🧱 ARQUITECTURA OBLIGATORIA

### Tipo de aplicación

- .NET 8 Console Application
- Proceso **long-running**
- Comunicación MCP por **STDIO**
- Mantiene el workspace cargado **en memoria**
- Se inicia y se detiene automáticamente cuando Gemini CLI inicia/termina

### Ciclo de vida

- El proceso se inicia con:
  `csharp-mcp-server --workspace <path>`
- Al iniciar:
  1. Detecta automáticamente `.sln` o `.csproj`
  2. Carga la solución usando `MSBuildWorkspace`
  3. Mantiene la `Solution` viva en memoria
- NO debe recargar la solución por request
- Debe soportar múltiples requests concurrentes (lectura paralela)
- No requiere hot-reload de archivos (snapshot estático es suficiente)

---

## 🧠 ROSLYN (REQUERIDO)

- Usar `Microsoft.CodeAnalysis`
- Usar `MSBuildWorkspace`
- Resolver símbolos con `SemanticModel`
- NO usar búsqueda textual
- Usar `SymbolFinder` para referencias
- La `Solution` debe ser tratada como inmutable

---

## 🔧 TOOLS MCP A EXPONER (OBLIGATORIAS)

### 1. resolveSymbol

Input:

```json
{ "symbol": "string" }


Output:

{
  "kind": "Class | Interface | Method | Namespace",
  "name": "string",
  "namespace": "string",
  "file": "string",
  "line": number
}

2. getSymbolSource

Input:

{ "symbol": "string" }


Output:

{
  "file": "string",
  "source": "string"
}

3. findReferences

Input:

{ "symbol": "string" }


Output:

[
  { "file": "string", "line": number }
]

4. listMembers

Input:

{ "symbol": "string" }


Output:

{
  "methods": ["string"],
  "properties": ["string"],
  "fields": ["string"]
}

🔌 MCP PROTOCOL

Implementar handshake MCP completo

Declarar herramientas con name, description y inputSchema

Responder estrictamente en JSON válido

Manejar errores con mensajes claros

🤝 INTEGRACIÓN CON GEMINI CLI (CRÍTICO)

Generar:

Ejemplo de configuración .gemini/settings.json:

{
  "mcpServers": {
    "csharp": {
      "command": "csharp-mcp-server",
      "args": ["--workspace", "${workspaceFolder}"],
      "trust": true
    }
  }
}




Gemini CLI inicia el MCP como proceso hijo

La comunicación es por STDIO

El lifecycle es automático

📦 ENTREGABLES OBLIGATORIOS

Código completo del proyecto .NET

.csproj

Estructura de carpetas clara

README.md con:

qué hace la tool

cómo compilar

cómo usarla con Gemini CLI

ejemplos reales de uso desde Gemini

Ejemplos de requests/responses MCP

Comentarios claros en el código

❌ NO HACER

No usar HTTP

No recargar la solución por request

No usar búsqueda textual

No depender de APIs internas de Gemini

✅ EXPECTATIVA FINAL

El resultado debe ser un Language Server para agentes, usable inmediatamente como tool MCP por Gemini CLI, capaz de proveer IntelliSense real sobre soluciones C# grandes, sin hacks ni workarounds.

Entrega el código completo y funcional.


```
