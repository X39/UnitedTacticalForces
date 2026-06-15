export function highlight() {
    if (typeof document === 'undefined' || !globalThis.hljs?.highlightElement) {
        return;
    }

    var codeTagList = document.querySelectorAll('pre code');
    for (var i = 0; i < codeTagList.length; i++) {
        globalThis.hljs.highlightElement(codeTagList[i]);
    }
}

export function addCopyButton() {
    if (typeof document === 'undefined' || typeof navigator === 'undefined' || !navigator.clipboard?.writeText) {
        return;
    }

    var snippets = document.querySelectorAll('.snippet');
    for (var i = 0; i < snippets.length; i++) {
        let snippet = snippets[i];
        let code = snippet.querySelector('code');
        if (!code || snippet.querySelector('.hljs-copy')) {
            continue;
        }

        let button = document.createElement('button');
        button.type = 'button';
        button.className = 'hljs-copy';
        button.textContent = 'Copy';
        button.addEventListener('click', async function () {
            try {
                await navigator.clipboard.writeText(code.innerText || code.textContent || '');
                this.textContent = 'Copied!';
            } catch {
                this.textContent = 'Copy failed';
            }

            var clickedButton = this;
            setTimeout(function () {
                clickedButton.textContent = 'Copy';
            }, 1000);
        });

        snippet.appendChild(button);
    }
}
