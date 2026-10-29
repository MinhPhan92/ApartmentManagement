// Global Search & Header Notification Controllers
document.addEventListener('DOMContentLoaded', function () {
    // ==========================================
    // 1. GLOBAL SEARCH CONTROLLER (BUG-01)
    // ==========================================
    const searchInput = document.getElementById('globalSearchInput');
    const searchDropdown = document.getElementById('globalSearchDropdown');
    const searchSpinner = document.getElementById('globalSearchSpinner');
    const searchContainer = document.getElementById('globalSearchContainer');
    const searchBtn = document.getElementById('globalSearchBtn');

    if (searchInput && searchDropdown && searchContainer) {
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

        async function executeSearchNow() {
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
                searchInput.focus();
                return;
            }

            if (searchSpinner) searchSpinner.classList.remove('d-none');

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
        }

        // Real-time input debounced search
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

            debounceTimer = setTimeout(function () {
                executeSearchNow();
            }, 300);
        });

        // Single-channel form submit for search (handles both search button click and Enter key)
        searchContainer.addEventListener('submit', function (e) {
            e.preventDefault();
            executeSearchNow();
        });

        // Keyboard navigation
        searchInput.addEventListener('keydown', function (e) {
            const items = searchDropdown.querySelectorAll('.search-result-item');
            const isDropdownVisible = !searchDropdown.classList.contains('d-none') && items.length > 0;

            if (e.key === 'Escape') {
                e.preventDefault();
                hideDropdown();
                return;
            }

            if (e.key === 'ArrowDown' && isDropdownVisible) {
                e.preventDefault();
                selectedIndex = (selectedIndex + 1) % items.length;
                updateActiveItem(items);
            } else if (e.key === 'ArrowUp' && isDropdownVisible) {
                e.preventDefault();
                selectedIndex = (selectedIndex - 1 + items.length) % items.length;
                updateActiveItem(items);
            } else if (e.key === 'Enter' && isDropdownVisible && selectedIndex >= 0 && selectedIndex < items.length) {
                // If an item in dropdown is focused via arrow keys, select it instead of submitting
                e.preventDefault();
                items[selectedIndex].click();
            }
            // If selectedIndex < 0, Enter naturally triggers searchContainer 'submit' event once
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
    }

    // ==========================================
    // 2. NOTIFICATION BELL CONTROLLER (BUG-02)
    // ==========================================
    const notificationBellBtn = document.getElementById('notificationBellBtn');
    const notificationBadge = document.getElementById('notificationBadge');
    const notificationCountBadge = document.getElementById('notificationCountBadge');
    const notificationList = document.getElementById('notificationList');

    if (notificationBellBtn && notificationList) {
        let isLoaded = false;

        async function fetchRecentNotifications() {
            try {
                const response = await fetch('/api/announcements/recent', {
                    method: 'GET',
                    headers: { 'Accept': 'application/json' }
                });

                if (!response.ok) {
                    throw new Error('HTTP error ' + response.status);
                }

                const notifications = await response.json();
                renderNotifications(notifications);
                isLoaded = true;
            } catch (err) {
                renderNotificationError();
            }
        }

        function getCategoryBadgeClass(category) {
            switch (category) {
                case 'Bảo trì':
                    return 'bg-warning-subtle text-warning-emphasis border border-warning-subtle';
                case 'Thu phí':
                    return 'bg-danger-subtle text-danger border border-danger-subtle';
                case 'An ninh':
                    return 'bg-info-subtle text-info-emphasis border border-info-subtle';
                default:
                    return 'bg-primary-subtle text-primary border border-primary-subtle';
            }
        }

        function renderNotifications(items) {
            if (!notificationList) return;
            notificationList.innerHTML = '';

            const count = items ? items.length : 0;

            if (notificationCountBadge) {
                notificationCountBadge.textContent = count > 0 ? (count + ' tin gần nhất') : '0 tin';
            }

            if (notificationBadge) {
                if (count > 0) {
                    notificationBadge.classList.remove('d-none');
                } else {
                    notificationBadge.classList.add('d-none');
                }
            }

            if (count === 0) {
                notificationList.innerHTML = `
                    <div class="p-4 text-center text-muted small">
                        <i class="bi bi-bell-slash fs-4 d-block mb-1 text-secondary opacity-50"></i>
                        <span>Chưa có thông báo mới nào.</span>
                    </div>
                `;
                return;
            }

            items.forEach(function (item) {
                const link = document.createElement('a');
                link.className = 'notification-item';
                link.setAttribute('href', item.url);

                const titleDiv = document.createElement('div');
                titleDiv.className = 'notification-item-title';
                titleDiv.textContent = (item.isPinned ? '📌 ' : '') + item.title;
                link.appendChild(titleDiv);

                const metaDiv = document.createElement('div');
                metaDiv.className = 'notification-item-meta';

                const badge = document.createElement('span');
                badge.className = 'badge ' + getCategoryBadgeClass(item.category);
                badge.style.fontSize = '0.65rem';
                badge.textContent = item.category;
                metaDiv.appendChild(badge);

                const dateSpan = document.createElement('span');
                dateSpan.textContent = item.displayDate;
                metaDiv.appendChild(dateSpan);

                link.appendChild(metaDiv);
                notificationList.appendChild(link);
            });
        }

        function renderNotificationError() {
            if (!notificationList) return;
            notificationList.innerHTML = `
                <div class="p-3 text-center text-danger small">
                    <i class="bi bi-exclamation-triangle me-1"></i>
                    <span>Không thể tải thông báo. Vui lòng thử lại.</span>
                </div>
            `;
        }

        // Fetch notifications once on page load
        fetchRecentNotifications();

        // Refresh when user opens dropdown if needed
        notificationBellBtn.addEventListener('click', function () {
            if (!isLoaded) {
                fetchRecentNotifications();
            }
        });
    }
});
