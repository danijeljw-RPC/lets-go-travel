const sessions = new Map();

export function createHostedPayment(elementId, browserToken) {
    const host = document.getElementById(elementId);
    if (!host) {
        throw new Error("Hosted payment container was not found.");
    }

    host.replaceChildren();
    const notice = document.createElement("p");
    notice.className = "hosted-payment-notice";
    notice.textContent = "Sandbox hosted payment is ready. Payment details remain with the payment provider.";
    host.appendChild(notice);

    const sessionHandle = crypto.randomUUID();
    sessions.set(sessionHandle, {
        elementId,
        completionReference: `fixture_${browserToken}_${crypto.randomUUID()}`,
    });
    return sessionHandle;
}

export function completeHostedPayment(sessionHandle) {
    const session = sessions.get(sessionHandle);
    if (!session) {
        throw new Error("Hosted payment session was not found.");
    }

    return {
        completionReference: session.completionReference,
    };
}

export function disposeHostedPayment(sessionHandle) {
    const session = sessions.get(sessionHandle);
    const host = session ? document.getElementById(session.elementId) : null;
    host?.replaceChildren();
    sessions.delete(sessionHandle);
}
