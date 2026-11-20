/**
 * A conversa com o servidor. Tudo passa por aqui: o cookie de sessão vai
 * sozinho (mesma origem), e todo pedido que muda algo leva o cabeçalho
 * X-Painel, sem o qual o servidor recusa.
 */
export class ApiError extends Error {
	constructor(
		public status: number,
		public code: string,
		message: string,
	) {
		super(message);
	}
}

/** Chamado quando o servidor diz que a sessão acabou. */
let onSignedOut: () => void = () => {};
export function setOnSignedOut(handler: () => void) {
	onSignedOut = handler;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
	let response: Response;
	try {
		response = await fetch(`/admin/v1${path}`, {
			method,
			credentials: "same-origin",
			headers: method === "GET" ? undefined : { "Content-Type": "application/json", "X-Painel": "1" },
			body: method === "GET" ? undefined : JSON.stringify(body ?? {}),
		});
	} catch {
		throw new ApiError(0, "sem_conexao", "Sem resposta do servidor. Confira se ele está de pé.");
	}

	const text = await response.text();
	let data: unknown = null;
	try {
		data = text ? JSON.parse(text) : null;
	} catch {
		// Resposta que não é JSON: cai no erro genérico abaixo.
	}

	if (!response.ok) {
		const error = (data as { error?: { code?: string; message?: string } } | null)?.error;
		if (response.status === 401 && error?.code === "sem_login") onSignedOut();
		throw new ApiError(response.status, error?.code ?? "erro", error?.message ?? `O servidor respondeu ${response.status}.`);
	}
	return data as T;
}

export const api = {
	get: <T>(path: string) => request<T>("GET", path),
	post: <T = { ok: true }>(path: string, body?: unknown) => request<T>("POST", path, body),
	put: <T = { ok: true }>(path: string, body?: unknown) => request<T>("PUT", path, body),
	delete: <T = { ok: true }>(path: string) => request<T>("DELETE", path),
};

export function messageOf(error: unknown) {
	return error instanceof Error ? error.message : "Algo deu errado.";
}
