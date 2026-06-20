// Accessible audio preview player.
// Enhances every .audio-preview block: play/pause, scrubbing, a hard length cap so the
// full track is never exposed, single-at-a-time playback, and screen-reader announcements.
(function () {
    "use strict";

    var players = [];

    function formatTime(seconds) {
        if (!isFinite(seconds) || seconds < 0) {
            seconds = 0;
        }
        var mins = Math.floor(seconds / 60);
        var secs = Math.floor(seconds % 60);
        return mins + ":" + (secs < 10 ? "0" : "") + secs;
    }

    function pauseOthers(active) {
        players.forEach(function (player) {
            if (player !== active) {
                player.audio.pause();
            }
        });
    }

    function setupPlayer(root) {
        var audio = root.querySelector("audio");
        var toggle = root.querySelector("[data-role='toggle']");
        var scrubber = root.querySelector("[data-role='scrubber']");
        var currentEl = root.querySelector("[data-role='current']");
        var durationEl = root.querySelector("[data-role='duration']");
        var statusEl = root.querySelector("[data-role='status']");

        if (!audio || !toggle || !scrubber) {
            return;
        }

        var title = root.getAttribute("data-title") || "this track";
        var cap = parseFloat(root.getAttribute("data-preview-seconds"));
        if (!isFinite(cap) || cap <= 0) {
            cap = 30;
        }

        // The longest the user can ever hear. Updated once real metadata loads.
        var limit = cap;

        var player = { root: root, audio: audio };
        players.push(player);

        function announce(message) {
            if (statusEl) {
                statusEl.textContent = message;
            }
        }

        function setLimit(value) {
            limit = Math.max(0, value);
            scrubber.max = limit.toFixed(2);
            if (durationEl) {
                durationEl.textContent = formatTime(limit);
            }
        }

        function reflectTime() {
            var t = Math.min(audio.currentTime, limit);
            scrubber.value = t.toFixed(2);
            scrubber.setAttribute("aria-valuetext", formatTime(t) + " of " + formatTime(limit));
            if (currentEl) {
                currentEl.textContent = formatTime(t);
            }
        }

        function showPlaying(isPlaying) {
            toggle.setAttribute("aria-pressed", isPlaying ? "true" : "false");
            toggle.setAttribute("aria-label", (isPlaying ? "Pause preview of " : "Play preview of ") + title);
            root.classList.toggle("is-playing", isPlaying);
            var icon = toggle.querySelector(".glyphicon");
            if (icon) {
                icon.classList.toggle("glyphicon-play", !isPlaying);
                icon.classList.toggle("glyphicon-pause", isPlaying);
            }
        }

        setLimit(cap);
        reflectTime();
        showPlaying(false);

        audio.addEventListener("loadedmetadata", function () {
            if (isFinite(audio.duration) && audio.duration > 0) {
                setLimit(Math.min(audio.duration, cap));
            }
            reflectTime();
        });

        toggle.addEventListener("click", function () {
            if (audio.paused) {
                if (audio.currentTime >= limit - 0.05) {
                    audio.currentTime = 0;
                }
                pauseOthers(player);
                var playPromise = audio.play();
                if (playPromise && typeof playPromise.catch === "function") {
                    playPromise.catch(function () {
                        announce("Preview could not be played.");
                    });
                }
            } else {
                audio.pause();
            }
        });

        audio.addEventListener("play", function () {
            showPlaying(true);
            announce("Playing preview of " + title + ".");
        });

        audio.addEventListener("pause", function () {
            showPlaying(false);
        });

        audio.addEventListener("timeupdate", function () {
            // Enforce the preview window so the full track is never exposed.
            if (audio.currentTime >= limit) {
                audio.pause();
                audio.currentTime = limit;
                reflectTime();
                announce("Preview ended.");
                return;
            }
            reflectTime();
        });

        audio.addEventListener("ended", function () {
            showPlaying(false);
            reflectTime();
        });

        audio.addEventListener("error", function () {
            announce("Preview unavailable.");
            toggle.disabled = true;
            scrubber.disabled = true;
            root.classList.add("is-unavailable");
        });

        scrubber.addEventListener("input", function () {
            var target = Math.min(parseFloat(scrubber.value) || 0, limit);
            audio.currentTime = target;
            reflectTime();
        });
    }

    function init() {
        var roots = document.querySelectorAll(".audio-preview");
        Array.prototype.forEach.call(roots, setupPlayer);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
