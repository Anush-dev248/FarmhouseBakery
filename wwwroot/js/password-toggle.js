// Adds a show / hide eye icon to every password box on the page.
(function () {
    const eyeOpen =
        '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"></path>' +
        '<circle cx="12" cy="12" r="3"></circle></svg>';

    const eyeClosed =
        '<svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">' +
        '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94"></path>' +
        '<path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19"></path>' +
        '<path d="M14.12 14.12a3 3 0 1 1-4.24-4.24"></path>' +
        '<line x1="1" y1="1" x2="23" y2="23"></line></svg>';

    const style = document.createElement("style");
    style.textContent =
        ".pw-toggle-wrap { position: relative; display: block; }" +
        ".pw-toggle-wrap input { padding-right: 44px !important; box-sizing: border-box; }" +
        ".pw-toggle-wrap .pw-toggle-btn {" +
        "  all: unset; position: absolute; right: 12px; top: 50%; transform: translateY(-50%);" +
        "  display: flex; align-items: center; justify-content: center;" +
        "  width: 28px; height: 28px; color: #888; cursor: pointer; border-radius: 4px; }" +
        ".pw-toggle-wrap .pw-toggle-btn:hover { color: #d89500; }" +
        ".pw-toggle-wrap .pw-toggle-btn:focus-visible { outline: 2px solid #ffbe33; }";
    document.head.appendChild(style);

    document.querySelectorAll('input[type="password"]').forEach(function (input) {
        const wrap = document.createElement("span");
        wrap.className = "pw-toggle-wrap";
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);

        const button = document.createElement("button");
        button.type = "button";
        button.className = "pw-toggle-btn";
        button.innerHTML = eyeOpen;
        button.setAttribute("aria-label", "Show password");
        wrap.appendChild(button);

        button.addEventListener("click", function () {
            const show = input.type === "password";
            input.type = show ? "text" : "password";
            button.innerHTML = show ? eyeClosed : eyeOpen;
            button.setAttribute("aria-label", show ? "Hide password" : "Show password");
            input.focus();
        });
    });
})();
