"""Leitura de C# o bastante para achar os textos: separa código, comentários e literais de string."""
import re


def tokens(src):
    """Gera (tipo, texto, início, fim): tipo 'code', 'comment', 'str' (comum), 'verb' (@"..."), 'interp' ($"...")."""
    i, n = 0, len(src)
    start = 0
    out = []

    def flush(end):
        if end > start:
            out.append(("code", src[start:end], start, end))

    while i < n:
        c = src[i]
        two = src[i:i + 2]
        if two == "//":
            flush(i)
            j = src.find("\n", i)
            j = n if j < 0 else j
            out.append(("comment", src[i:j], i, j)); i = start = j
        elif two == "/*":
            flush(i)
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            out.append(("comment", src[i:j], i, j)); i = start = j
        elif c == "'" :
            # caractere: 'a' ou '\n'
            j = i + 1
            if j < n and src[j] == "\\": j += 2
            else: j += 1
            if j < n and src[j] == "'":
                i = j + 1
            else:
                i += 1
        elif c == '"' or (c in "$@" and re.match(r'[$@]{1,2}"', src[i:i + 3])):
            flush(i)
            m = re.match(r'[$@]*', src[i:])
            prefix = m.group(0)
            j = i + len(prefix)
            if src[j:j + 3] == '"""':
                k = src.find('"""', j + 3)
                k = n if k < 0 else k + 3
                out.append(("verb", src[i:k], i, k)); i = start = k
                continue
            verbatim = "@" in prefix
            interp = "$" in prefix
            j += 1
            depth = 0
            while j < n:
                ch = src[j]
                if verbatim:
                    if ch == '"':
                        if src[j + 1:j + 2] == '"': j += 2; continue
                        if depth == 0: break
                elif ch == "\\" and depth == 0:
                    j += 2; continue
                elif ch == '"' and depth == 0:
                    break
                if interp:
                    if ch == "{":
                        if src[j + 1:j + 2] == "{" and depth == 0: j += 2; continue
                        depth += 1
                    elif ch == "}":
                        if depth == 0 and src[j + 1:j + 2] == "}": j += 2; continue
                        depth = max(depth - 1, 0)
                    elif ch == '"' and depth > 0:
                        # string dentro do buraco: pula até fechar
                        k = j + 1
                        while k < n and src[k] != '"':
                            k += 2 if src[k] == "\\" else 1
                        j = k
                if ch == "\n" and not verbatim and depth == 0:
                    break
                j += 1
            kind = "verb" if verbatim and not interp else "interp" if interp else "str"
            out.append((kind, src[i:j + 1], i, j + 1)); i = start = j + 1
        else:
            i += 1
    flush(n)
    return out


def unescape(body):
    return (body.replace('\\"', '"').replace("\\n", "\n").replace("\\t", "\t").replace("\\\\", "\\"))


def escape(text):
    return text.replace("\\", "\\\\").replace('"', '\\"').replace("\n", "\\n").replace("\t", "\\t")


def split_interp(lit):
    """$"a {x:0.0} b" -> ("a {0:0.0} b", ["x"]) ou None se algum buraco for complicado."""
    assert lit.startswith('$"') and lit.endswith('"')
    body = lit[2:-1]
    fmt, args = [], []
    i, n = 0, len(body)
    while i < n:
        ch = body[i]
        if ch == "{":
            if body[i + 1:i + 2] == "{": fmt.append("{{"); i += 2; continue
            depth, j = 1, i + 1
            while j < n and depth:
                if body[j] == "{": depth += 1
                elif body[j] == "}": depth -= 1
                elif body[j] == '"': return None
                j += 1
            hole = body[i + 1:j - 1]
            # separa o formato (":0.0") fora de parênteses e de ?:
            expr, spec = hole, ""
            d = 0; cut = -1
            for k, c in enumerate(hole):
                if c in "([": d += 1
                elif c in ")]": d -= 1
                elif c == ":" and d == 0 and "?" not in hole[:k]: cut = k; break
                elif c == "," and d == 0 and "?" not in hole[:k]: cut = k; break
            if cut >= 0: expr, spec = hole[:cut], hole[cut:]
            if not expr.strip(): return None
            fmt.append("{%d%s}" % (len(args), spec)); args.append(expr.strip()); i = j
        elif ch == "}":
            if body[i + 1:i + 2] == "}": fmt.append("}}"); i += 2; continue
            return None
        else:
            if ch == "\\": fmt.append(body[i:i + 2]); i += 2
            else: fmt.append(ch); i += 1
    return "".join(fmt), args
