/**
 * Report Viewer JS helpers.
 * Scroll detection and page navigation for the virtualised viewer.
 */

/**
 * Returns the page number of the page-wrapper div most visible in the scroll container.
 * Each page wrapper has id="rv-pw-{pageNum}".
 */
window.rvGetCurrentPage = function(scrollContainerId) {
    const container = document.getElementById(scrollContainerId);
    if (!container) return 0;

    const containerTop    = container.scrollTop;
    const containerBottom = containerTop + container.clientHeight;
    let   bestPage        = 0;
    let   bestVisible     = 0;

    // Walk all rv-pw-* children of the page list
    const list = container.querySelector('.rv-page-list');
    if (!list) return 0;

    for (const child of list.children) {
        if (!child.id || !child.id.startsWith('rv-pw-')) continue;
        const pageNum   = parseInt(child.id.replace('rv-pw-', ''), 10);
        const childTop  = child.offsetTop;
        const childBot  = childTop + child.offsetHeight;
        const visible   = Math.max(0, Math.min(childBot, containerBottom) - Math.max(childTop, containerTop));
        if (visible > bestVisible) {
            bestVisible = visible;
            bestPage    = pageNum;
        }
    }
    return bestPage;
};

/**
 * Scrolls the container so the given page wrapper is at the top.
 */
window.rvScrollToPage = function(scrollContainerId, pageWrapperId) {
    const container = document.getElementById(scrollContainerId);
    const pageEl    = document.getElementById(pageWrapperId);
    if (!container || !pageEl) return;
    container.scrollTo({ top: pageEl.offsetTop - 8, behavior: 'smooth' });
};
