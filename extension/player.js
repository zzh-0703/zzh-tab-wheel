// Chrome serializes this function when injecting it into the tab.
export function autoplayVideo(expectedUrl) {
    if (location.href !== expectedUrl) {
        return;
    }

    const playerSelector = ".dplayer, .video-js, .plyr, .art-video-player, .jwplayer";
    const playControls = [
        ".dplayer-play-icon",
        ".dplayer-mobile-play",
        ".vjs-big-play-button",
        ".plyr__control--overlaid[data-plyr='play']",
        ".art-control-play",
        ".jw-display-icon-play",
        "button[aria-label='Play' i]",
        "button[aria-label='播放']",
    ].join(", ");
    const webFullscreenControls = [
        ".dplayer-full-in-icon",
        "[aria-label='网页全屏']",
        "[title='网页全屏']",
        "[data-balloon='网页全屏']",
        "[data-balloon='Web fullscreen']",
    ].join(", ");

    let scanTimer;
    const observer = new MutationObserver(() => {
        if (scanTimer === undefined) {
            scanTimer = setTimeout(scan, 150);
        }
    });
    const deadline = setTimeout(stopLooking, 20_000);
    observer.observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ["class", "style", "hidden", "src"],
    });
    scan();

    function stopLooking() {
        observer.disconnect();
        clearTimeout(deadline);
        clearTimeout(scanTimer);
    }

    function scan() {
        scanTimer = undefined;
        if (location.href !== expectedUrl) {
            stopLooking();
            return;
        }

        const target = findVideo();
        if (!target) {
            return;
        }

        stopLooking();
        const { video, player, button, webFullscreenButton } = target;
        video.muted = true;
        player.scrollIntoView({ block: "center", inline: "nearest", behavior: "instant" });
        openWebFullscreen(player, webFullscreenButton);

        const hasPreroll = player.classList.contains("dplayer-pre-playing");
        if (player.matches(".dplayer") && (video.paused || hasPreroll)) {
            keepDPlayerMuted(video, player);
        }

        if (video.paused && !hasPreroll) {
            if (button) {
                button.click();
            } else {
                playMuted(video);
            }
        }
    }

    function findVideo() {
        let best;
        let largestArea = 0;
        for (const video of document.querySelectorAll("video")) {
            const { width, height } = video.getBoundingClientRect();
            const area = width * height;
            if (width < 120 || height < 68 || area <= largestArea ||
                getComputedStyle(video).visibility !== "visible") {
                continue;
            }

            const player = video.closest(playerSelector);
            const controls = player || video.parentElement;
            const button = Array.from(controls.querySelectorAll(playControls))
                .find(control => !control.closest("a, [disabled], [aria-disabled='true']") &&
                    control.getClientRects().length > 0 &&
                    getComputedStyle(control).visibility === "visible");
            const webFullscreenButton = controls.querySelector(webFullscreenControls);
            if (!button && !video.currentSrc && !video.srcObject &&
                !video.getAttribute("src") && !video.querySelector("source[src]")) {
                continue;
            }

            best = { video, player: player || video, button, webFullscreenButton };
            largestArea = area;
        }
        return best;
    }

    function openWebFullscreen(player, button) {
        if (!button || player.classList.contains("dplayer-fulled")) {
            return;
        }

        button.click();
    }

    function playMuted(video) {
        video.play().catch(() => {
            // The player or browser may require a manual click.
        });
    }

    function keepDPlayerMuted(video, player) {
        const inPreroll = () => player.classList.contains("dplayer-pre-playing");
        let sawPreroll = inPreroll();
        const originalVolume = video.volume;
        // The preroll can restore the old mute flag before calling play() again.
        video.volume = 0;
        const prerollObserver = new MutationObserver(() => {
            if (inPreroll()) {
                sawPreroll = true;
            } else if (sawPreroll) {
                finish();
                if (video.paused) {
                    playMuted(video);
                }
            }
        });
        const timeout = setTimeout(finishWhenReady, 20_000);
        prerollObserver.observe(player, { attributes: true, attributeFilter: ["class"] });
        video.addEventListener("playing", finishWhenReady);

        function finishWhenReady() {
            if (!inPreroll()) {
                finish();
            }
        }

        function finish() {
            prerollObserver.disconnect();
            clearTimeout(timeout);
            video.removeEventListener("playing", finishWhenReady);
            video.muted = true;
            if (video.volume === 0) {
                video.volume = originalVolume;
            }
        }
    }
}
