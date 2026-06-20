(function () {
    "use strict";

    function pad(value) {
        return value < 10 ? "0" + value : String(value);
    }

    function format(msRemaining) {
        var totalSeconds = Math.floor(msRemaining / 1000);
        var days = Math.floor(totalSeconds / 86400);
        var hours = Math.floor((totalSeconds % 86400) / 3600);
        var minutes = Math.floor((totalSeconds % 3600) / 60);
        var seconds = totalSeconds % 60;

        if (days > 0) {
            return days + "d " + pad(hours) + ":" + pad(minutes) + ":" + pad(seconds);
        }

        return pad(hours) + ":" + pad(minutes) + ":" + pad(seconds);
    }

    function startCountdown(panel) {
        var clock = panel.querySelector("[data-deal-countdown]");
        var endsAttr = panel.getAttribute("data-deal-ends-utc");
        if (!clock || !endsAttr) {
            return;
        }

        var endsAt = Date.parse(endsAttr);
        if (isNaN(endsAt)) {
            return;
        }

        function tick() {
            var remaining = endsAt - Date.now();
            if (remaining <= 0) {
                clock.textContent = "Deal ended";
                panel.classList.add("deal-expired");
                window.clearInterval(timer);
                return;
            }

            clock.textContent = format(remaining);
        }

        tick();
        var timer = window.setInterval(tick, 1000);
    }

    function init() {
        var panels = document.querySelectorAll(".deal-countdown[data-deal-ends-utc]");
        for (var i = 0; i < panels.length; i++) {
            startCountdown(panels[i]);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
}());
