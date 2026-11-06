// The client layer of the screens (decision 0010, 0052): swaps a region the markup declares, moves the focus after it
// and fires a shortcut the markup declares. It decides nothing: every rule of the system answers on the server.
(function () {
    "use strict";

    var REGION_HEADER = "X-Region";
    var TEXT_FIELDS = ["INPUT", "TEXTAREA", "SELECT"];

    function typing(target) {
        return target instanceof Element && (TEXT_FIELDS.indexOf(target.tagName) >= 0 || target.isContentEditable);
    }

    function focusIn(region) {
        var selector = region.getAttribute("data-focus");
        var target = (selector && region.querySelector(selector)) || region;
        target.focus();
    }

    function swapIn(id, html) {
        var received = new DOMParser().parseFromString(html, "text/html").getElementById(id);
        var current = document.getElementById(id);
        if (!received || !current) {
            return false;
        }
        current.replaceWith(received);
        focusIn(received);
        return true;
    }

    function request(url, init, id) {
        var region = document.getElementById(id);
        if (region) {
            region.setAttribute("aria-busy", "true");
        }
        init.headers = init.headers || {};
        init.headers[REGION_HEADER] = id;
        init.credentials = "same-origin";
        return fetch(url, init).then(function (response) {
            return response.text();
        }).then(function (html) {
            return swapIn(id, html);
        }).catch(function () {
            return false;
        }).then(function (swapped) {
            var after = document.getElementById(id);
            if (after) {
                after.removeAttribute("aria-busy");
            }
            return swapped;
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
        request(url, init, id).then(function (swapped) {
            if (!swapped) {
                HTMLFormElement.prototype.submit.call(form);
            }
        });
    });

    document.addEventListener("click", function (event) {
        var link = event.target instanceof Element ? event.target.closest("a[data-swap]") : null;
        if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) {
            return;
        }
        event.preventDefault();
        request(link.href, { method: "GET" }, link.getAttribute("data-swap")).then(function (swapped) {
            if (!swapped) {
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
