// Purpose: Implements onboarding web behavior for wwwroot.
export function getBoolean(key) {
    try {
        return window.localStorage.getItem(key) === 'true';
    } catch {
        return false;
    }
}

export function setBoolean(key, value) {
    try {
        window.localStorage.setItem(key, value ? 'true' : 'false');
    } catch {
        // Browser privacy/storage settings may intentionally block persistence.
    }
}

export function removeValue(key) {
    try {
        window.localStorage.removeItem(key);
    } catch {
        // Treat storage unavailability as non-fatal UI state.
    }
}
