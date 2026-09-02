import { invoke } from "@tauri-apps/api/core";

export const API_BASE_URL = "http://localhost:5213";

let apiKeyPromise: Promise<string> | null = null;

async function getApiKey(): Promise<string> {
    if (!apiKeyPromise) {
        apiKeyPromise = (async () => {
            const developmentKey = import.meta.env.VITE_LIGHTSHIELD_API_KEY as string | undefined;
            if (developmentKey) return developmentKey;
            return await invoke<string>("get_api_key");
        })();
    }
    return apiKeyPromise;
}

export async function apiFetch(path: string, init: RequestInit = {}): Promise<Response> {
    const apiKey = await getApiKey();
    const headers = new Headers(init.headers);
    headers.set("X-LightShield-Api-Key", apiKey);
    return fetch(`${API_BASE_URL}${path}`, { ...init, headers });
}
