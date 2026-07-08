# Guardrails Engine

The Guardrails Engine inside `Asterran.Engine.Guardrails` provides language-abstracted security scans. It isolates guardrail rule logic from the differences in syntax between programming languages.

---

## 1. How Language Abstraction Works

Instead of parsing full AST (Abstract Syntax Trees) which are language-specific, Asterran breaks source files into a unified stream of generic tokens:

```
[Source File] ──> [Language Lexer] ──> [Generic Tokens Stream] ──> [Shared Guardrail Rules]
```

### Generic Token Types (`GenericToken.cs`)
All language tokenizers translate source syntax into standard tokens:
- `Keyword`: Language reserved terms (e.g., `let`, `import`, `class`).
- `Identifier`: Names of variables, functions, or namespaces (e.g., `apiKey`, `md5`).
- `StringLiteral`: Text values enclosed in quotes.
- `NumberLiteral`: Integers and floating-point constants.
- `Operator` / `Symbol` / `Comment`: Punctuation and notes.

---

## 2. Supported Languages

Concrete implementations of `IGenericLexer` tokenise files using regular expression matching:

1. **`CSharpLexer`**: Handles C# namespaces, var types, and string formats.
2. **`PythonLexer`**: Supports Python's triple-quotes, raw strings, comments, and the walrus operator (`:=`).
3. **`JsLexer`**: Handles JavaScript and TypeScript template strings and syntax.

---

## 3. Implemented Guardrail Rules

Rules implement the `IGuardrailRule` contract, evaluating the generic token stream:

### Secrets & Credentials Scanner (`SecretsRule.cs`)
- **What it flags**: Assignments of string values to variables representing access credentials.
- **Matching pattern**: Looks for variable identifiers containing patterns like `key`, `token`, `secret`, `password`, `credential`, or `auth` followed by a assignment operator and a string literal value.

### Process Command Execution Scanner (`CommandExecutionRule.cs`)
- **What it flags**: Spawning shell commands or executing external operating system processes.
- **Matching pattern**: Looks for invocation identifiers representing process starts (such as `Process.Start` in C#, `subprocess.run` or `os.system` in Python, or `child_process` calls in NodeJs).

### Obsolete Cryptography Scanner (`ObsoleteCryptoRule.cs`)
- **What it flags**: References to deprecated, cryptographically weak hashing algorithms or ciphers.
- **Matching pattern**: Detects identifiers or strings referencing obsolete standard protocols (such as `MD5`, `DES`, `RC4`, or `SHA1`).
