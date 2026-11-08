// The client layer of the screens (decision 0010, 0052): swaps a region the markup declares, moves the focus after it
// and fires a shortcut the markup declares. It decides nothing: every rule of the system answers on the server.
(function () {
    "use strict";

    // A key typed in a field that takes text belongs to the field; a box to mark, a choice or a button takes no text, so a shortcut still fires there.
    var TEXT_FIELDS = ["TEXTAREA", "SELECT"];
    var INPUTS_WITHOUT_TEXT = ["checkbox", "radio", "button", "submit", "reset", "range", "color", "file", "image"];

    function typing(target) {
        if (!(target instanceof Element)) {
            return false;
        }
        if (target.tagName === "INPUT") {
            return INPUTS_WITHOUT_TEXT.indexOf(target.type) < 0;
        }
        return TEXT_FIELDS.indexOf(target.tagName) >= 0 || target.isContentEditable;
    }

    function focusIn(region) {
        var selector = region.getAttribute("data-focus");
        var target = (selector && region.querySelector(selector)) || region;
        target.focus();
    }

    function swapIn(id, received) {
        var region = received.getElementById(id);
        var current = document.getElementById(id);
        if (!region || !current) {
            return false;
        }
        current.replaceWith(document.adoptNode(region));
        focusIn(region);
        return true;
    }

    // The server always answers the whole page; an answer without the region is shown whole, frame and all; a redirect is followed by a plain load of where it points.
    // Once the server answered, the request is never sent again: it has already done what was asked.
    function showWhole(response, received) {
        if (response.redirected) {
            location.assign(response.url);
            return;
        }
        document.replaceChild(document.adoptNode(received.documentElement), document.documentElement);
        (document.getElementById("content") || document.body).focus();
    }

    // A swap a GET made changes what the page shows, so the address takes it (decision 0051): going back, and sharing the address, give the same region.
    // The entry the page was loaded on is marked with its region as well, so that going back to it restores the region as it was.
    function remember(id, url) {
        if (!history.state) {
            history.replaceState({ swap: id }, "", location.href);
        }
        history.pushState({ swap: id }, "", url);
    }

    // Resolves to true when nothing reached back from the server, so that only a request that changes nothing may be sent again as a plain load.
    // A request that is to be remembered puts its address in the history once its region is swapped in, and only then.
    function request(url, init, id, remembered) {
        var region = document.getElementById(id);
        if (region) {
            region.setAttribute("aria-busy", "true");
        }
        // The request asks for a page, as the browser would, so that the server answers a refusal or a failure with a page and never with a blank.
        init.credentials = "same-origin";
        init.headers = { Accept: "text/html" };
        return fetch(url, init).then(function (response) {
            return response.text().then(function (html) {
                var received = new DOMParser().parseFromString(html, "text/html");
                if (!swapIn(id, received)) {
                    showWhole(response, received);
                } else if (remembered) {
                    remember(id, response.redirected ? response.url : url);
                }
                return false;
            });
        }, function () {
            return true;
        }).then(function (unanswered) {
            var after = document.getElementById(id);
            if (after) {
                after.removeAttribute("aria-busy");
            }
            return unanswered;
        });
    }

    // A form that declares a waiting notice says, while it is on its way, that the answer is coming, and is not sent a second time meanwhile.
    // The text is the server's; the layer only shows it, and knows nothing of why the answer takes long.
    document.addEventListener("submit", function (event) {
        var form = event.target;
        var slot = form instanceof HTMLFormElement && form.hasAttribute("data-wait") ? document.getElementById(form.getAttribute("data-wait")) : null;
        if (!slot) {
            return;
        }
        if (form.getAttribute("aria-busy") === "true") {
            event.preventDefault();
            return;
        }
        form.setAttribute("aria-busy", "true");
        slot.textContent = slot.getAttribute("data-wait-text");
    });

    // A page brought back from the history of the browser is no longer waiting for anything.
    window.addEventListener("pageshow", function (event) {
        if (!event.persisted) {
            return;
        }
        document.querySelectorAll("form[data-wait][aria-busy]").forEach(function (form) {
            form.removeAttribute("aria-busy");
            var slot = document.getElementById(form.getAttribute("data-wait"));
            if (slot) {
                slot.textContent = "";
            }
        });
    });

    document.addEventListener("submit", function (event) {
        var form = event.target;
        var id = form instanceof HTMLFormElement ? form.getAttribute("data-swap") : null;
        if (!id || event.defaultPrevented) {
            return;
        }
        event.preventDefault();
        var method = (form.getAttribute("method") || "get").toUpperCase();
        var data = new FormData(form, event.submitter);
        var url = form.getAttribute("action") || location.href;
        var init = { method: method };
        if (method === "GET") {
            url = url.split("?")[0] + "?" + new URLSearchParams(data).toString();
        } else {
            init.body = new URLSearchParams(data);
        }
        request(url, init, id, method === "GET").then(function (unanswered) {
            if (unanswered && method === "GET") {
                location.assign(url);
            }
        });
    });

    document.addEventListener("click", function (event) {
        var link = event.target instanceof Element ? event.target.closest("a[data-swap]") : null;
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }
        event.preventDefault();
        request(link.href, { method: "GET" }, link.getAttribute("data-swap"), true).then(function (unanswered) {
            if (unanswered) {
                location.assign(link.href);
            }
        });
    });

    // Going back or forth to an entry a swap made asks the server for that address again and swaps the same region, without a new entry;
    // an entry the layer did not make, such as the jump to the content, is the browser's own.
    window.addEventListener("popstate", function (event) {
        var id = event.state && event.state.swap;
        if (!id) {
            return;
        }
        request(location.href, { method: "GET" }, id, false).then(function (unanswered) {
            if (unanswered) {
                location.reload();
            }
        });
    });

    document.addEventListener("keydown", function (event) {
        if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey || event.repeat || typing(event.target)) {
            return;
        }
        var matches = document.querySelectorAll("[data-shortcut]");
        for (var i = 0; i < matches.length; i++) {
            if (matches[i].getAttribute("data-shortcut") === event.key && !matches[i].disabled) {
                event.preventDefault();
                matches[i].click();
                return;
            }
        }
    });
})();
