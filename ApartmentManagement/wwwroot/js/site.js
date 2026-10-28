// Global Search Controller & UI Interactions
document.addEventListener('DOMContentLoaded', function () {
    const searchInput = document.getElementById('globalSearchInput');
    const searchDropdown = document.getElementById('globalSearchDropdown');
    const searchSpinner = document.getElementById('globalSearchSpinner');
    const searchContainer = document.getElementById('globalSearchContainer');

    if (!searchInput || !searchDropdown || !searchContainer) {
        return;
    }

    let debounceTimer = null;
    let activeAbortController = null;
    let selectedIndex = -1;

    function hideDropdown() {
        searchDropdown.classList.add('d-none');
        searchDropdown.innerHTML = '';
        selectedIndex = -1;
    }

    function showDropdown() {
        searchDropdown.classList.remove('d-none');
    }

    function renderEmpty(message) {
        searchDropdown.innerHTML = '';
        const emptyDiv = document.createElement('div');
        emptyDiv.className = 'search-empty-state';
        emptyDiv.textContent = message || 'Không tìm thấy kết quả nào phù hợp.';
        searchDropdown.appendChild(emptyDiv);
        showDropdown();
    }

    function renderError(message) {
        searchDropdown.innerHTML = '';
        const errDiv = document.createElement('div');
        errDiv.className = 'search-error-state text-danger';
        errDiv.textContent = message || 'Lỗi khi tìm kiếm. Vui lòng thử lại sau.';
        searchDropdown.appendChild(errDiv);
        showDropdown();
    }

    function renderResults(data) {
        searchDropdown.innerHTML = '';
        selectedIndex = -1;

        if (!data || !data.groups || data.groups.length === 0 || data.totalCount === 0) {
            renderEmpty('Không tìm thấy kết quả nào phù hợp.');
            return;
        }

        data.groups.forEach(function (group) {
            if (!group.items || group.items.length === 0) return;

            const header = document.createElement('div');
            header.className = 'search-group-header';
            header.textContent = group.category + ' (' + group.items.length + ')';
            searchDropdown.appendChild(header);

            group.items.forEach(function (item) {
                const link = document.createElement('a');
                link.className = 'search-result-item';
                link.setAttribute('href', item.url);
                link.setAttribute('role', 'option');

                const contentDiv = document.createElement('div');
                contentDiv.style.flex = '1';
                contentDiv.style.minWidth = '0';
                contentDiv.style.marginRight = '0.5rem';

                const titleEl = document.createElement('div');
                titleEl.className = 'search-result-title';
                titleEl.textContent = item.title;
                contentDiv.appendChild(titleEl);

                const subtitleEl = document.createElement('div');
                subtitleEl.className = 'search-result-subtitle';
                subtitleEl.textContent = item.subtitle;
                contentDiv.appendChild(subtitleEl);

                link.appendChild(contentDiv);

                if (item.badge) {
                    const badgeSpan = document.createElement('span');
                    badgeSpan.className = 'badge ' + (item.badgeClass || 'bg-secondary');
                    badgeSpan.style.fontSize = '0.7rem';
                    badgeSpan.textContent = item.badge;
                    link.appendChild(badgeSpan);
                }

                searchDropdown.appendChild(link);
            });
        });

        showDropdown();
    }

    searchInput.addEventListener('input', function () {
        const query = searchInput.value.trim();

        if (debounceTimer) {
            clearTimeout(debounceTimer);
            debounceTimer = null;
        }

        if (activeAbortController) {
            activeAbortController.abort();
            activeAbortController = null;
        }

        if (query.length < 2) {
            hideDropdown();
            if (searchSpinner) searchSpinner.classList.add('d-none');
            return;
        }

        if (searchSpinner) searchSpinner.classList.remove('d-none');

        debounceTimer = setTimeout(async function () {
            activeAbortController = new AbortController();
            try {
                const response = await fetch('/api/search?q=' + encodeURIComponent(query), {
                    method: 'GET',
                    headers: {
                        'Accept': 'application/json'
                    },
                    signal: activeAbortController.signal
                });

                if (!response.ok) {
                    throw new Error('Search failed with status: ' + response.status);
                }

                const data = await response.json();
                renderResults(data);
            } catch (err) {
                if (err.name !== 'AbortError') {
                    renderError('Không thể kết nối đến máy chủ. Vui lòng thử lại.');
                }
            } finally {
                if (searchSpinner) searchSpinner.classList.add('d-none');
                activeAbortController = null;
            }
        }, 300);
    });

    searchInput.addEventListener('keydown', function (e) {
        const items = searchDropdown.querySelectorAll('.search-result-item');
        if (items.length === 0 || searchDropdown.classList.contains('d-none')) {
            if (e.key === 'Escape') {
                hideDropdown();
            }
            return;
        }

        if (e.key === 'ArrowDown') {
            e.preventDefault();
            selectedIndex = (selectedIndex + 1) % items.length;
            updateActiveItem(items);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            selectedIndex = (selectedIndex - 1 + items.length) % items.length;
            updateActiveItem(items);
        } else if (e.key === 'Enter') {
            if (selectedIndex >= 0 && selectedIndex < items.length) {
                e.preventDefault();
                items[selectedIndex].click();
            }
        } else if (e.key === 'Escape') {
            e.preventDefault();
            hideDropdown();
        }
    });

    function updateActiveItem(items) {
        items.forEach(function (el, idx) {
            if (idx === selectedIndex) {
                el.classList.add('active');
                el.scrollIntoView({ block: 'nearest' });
            } else {
                el.classList.remove('active');
            }
        });
    }

    document.addEventListener('click', function (e) {
        if (!searchContainer.contains(e.target)) {
            hideDropdown();
        }
    });

    searchInput.addEventListener('focus', function () {
        if (searchDropdown.children.length > 0 && searchInput.value.trim().length >= 2) {
            showDropdown();
        }
    });
});
