// The client layer of the screens (decision 0010, 0052): swaps a region the markup declares, moves the focus after it
// and fires a shortcut the markup declares. It decides nothing: every rule of the system answers on the server.
(function () {
    "use strict";

    var TEXT_FIELDS = ["INPUT", "TEXTAREA", "SELECT"];

    function typing(target) {
        return target instanceof Element && (TEXT_FIELDS.indexOf(target.tagName) >= 0 || target.isContentEditable);
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

    // Resolves to true when nothing reached back from the server, so that only a request that changes nothing may be sent again as a plain load.
    function request(url, init, id) {
        var region = document.getElementById(id);
        if (region) {
            region.setAttribute("aria-busy", "true");
        }
        init.credentials = "same-origin";
        return fetch(url, init).then(function (response) {
            return response.text().then(function (html) {
                var received = new DOMParser().parseFromString(html, "text/html");
                if (!swapIn(id, received)) {
                    showWhole(response, received);
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
        request(url, init, id).then(function (unanswered) {
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
        request(link.href, { method: "GET" }, link.getAttribute("data-swap")).then(function (unanswered) {
            if (unanswered) {
                location.assign(link.href);
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
