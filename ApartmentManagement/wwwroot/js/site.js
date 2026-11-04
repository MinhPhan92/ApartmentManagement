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
    // 2. NOTIFICATION BELL & SIGNALR REALTIME CONTROLLER (APARTMENT-05A)
    // ==========================================
    const notificationBellBtn = document.getElementById('notificationBellBtn');
    const notificationBadge = document.getElementById('notificationBadge');
    const notificationCountBadge = document.getElementById('notificationCountBadge');
    const notificationList = document.getElementById('notificationList');
    const markAllReadBtn = document.getElementById('markAllReadBtn');
    const toastContainer = document.getElementById('notificationToastContainer');

    if (notificationBellBtn && notificationList) {
        let isLoaded = false;
        let currentUnreadCount = 0;

        function getCsrfToken() {
            const tokenInput = document.querySelector('#antiforgeryTokenForm input[name="__RequestVerificationToken"]');
            return tokenInput ? tokenInput.value : '';
        }

        function updateBadge(count) {
            currentUnreadCount = Math.max(0, count);
            if (notificationBadge) {
                if (currentUnreadCount > 0) {
                    notificationBadge.textContent = currentUnreadCount > 99 ? '99+' : currentUnreadCount;
                    notificationBadge.classList.remove('d-none');
                } else {
                    notificationBadge.textContent = '';
                    notificationBadge.classList.add('d-none');
                }
            }
            if (notificationCountBadge) {
                notificationCountBadge.textContent = currentUnreadCount > 0 ? (currentUnreadCount + ' chưa đọc') : '0 tin mới';
            }
        }

        function getTypeBadgeClass(type) {
            switch (type) {
                case 'Phí':
                    return 'bg-danger-subtle text-danger border border-danger-subtle';
                case 'Thanh toán':
                    return 'bg-success-subtle text-success border border-success-subtle';
                case 'Sửa chữa':
                    return 'bg-warning-subtle text-warning-emphasis border border-warning-subtle';
                case 'Phương tiện':
                    return 'bg-info-subtle text-info-emphasis border border-info-subtle';
                case 'Khách':
                    return 'bg-primary-subtle text-primary border border-primary-subtle';
                case 'Phản ánh':
                    return 'bg-secondary-subtle text-dark border border-secondary-subtle';
                default:
                    return 'bg-primary-subtle text-primary border border-primary-subtle';
            }
        }

        function createNotificationElement(item) {
            const link = document.createElement('a');
            link.className = 'notification-item d-block p-3 border-bottom text-decoration-none ' + (!item.isRead ? 'bg-primary-subtle bg-opacity-10' : '');
            link.setAttribute('href', item.targetUrl || '/Notifications');
            link.setAttribute('data-id', item.id || '');

            const titleDiv = document.createElement('div');
            titleDiv.className = 'notification-item-title fw-semibold text-dark small mb-1';
            titleDiv.textContent = item.title;
            link.appendChild(titleDiv);

            const contentDiv = document.createElement('div');
            contentDiv.className = 'text-muted small text-truncate mb-2';
            contentDiv.textContent = item.content;
            link.appendChild(contentDiv);

            const metaDiv = document.createElement('div');
            metaDiv.className = 'd-flex align-items-center justify-content-between';

            const badge = document.createElement('span');
            badge.className = 'badge ' + getTypeBadgeClass(item.type);
            badge.style.fontSize = '0.65rem';
            badge.textContent = item.type;
            metaDiv.appendChild(badge);

            const dateSpan = document.createElement('span');
            dateSpan.className = 'text-muted';
            dateSpan.style.fontSize = '0.7rem';
            dateSpan.textContent = item.displayDate;
            metaDiv.appendChild(dateSpan);

            link.appendChild(metaDiv);
            return link;
        }

        function renderNotifications(data) {
            if (!notificationList) return;
            notificationList.innerHTML = '';

            const items = data && data.items ? data.items : [];
            updateBadge(data && typeof data.unreadCount === 'number' ? data.unreadCount : items.filter(x => !x.isRead).length);

            if (items.length === 0) {
                notificationList.innerHTML = `
                    <div class="p-4 text-center text-muted small">
                        <i class="bi bi-bell-slash fs-4 d-block mb-1 text-secondary opacity-50"></i>
                        <span>Chưa có thông báo nào.</span>
                    </div>
                `;
                return;
            }

            items.forEach(function (item) {
                notificationList.appendChild(createNotificationElement(item));
            });
        }

        function prependRealtimeNotification(item) {
            if (!notificationList) return;
            const emptyState = notificationList.querySelector('.text-center');
            if (emptyState) {
                notificationList.innerHTML = '';
            }

            const el = createNotificationElement(item);
            notificationList.insertBefore(el, notificationList.firstChild);

            // Giới hạn hiển thị 8 tin gần nhất trong dropdown
            while (notificationList.children.length > 8) {
                notificationList.removeChild(notificationList.lastChild);
            }
        }

        function showNotificationToast(item) {
            if (!toastContainer) return;

            const toastId = 'toast_' + Date.now();
            const toastHtml = `
                <div id="${toastId}" class="toast align-items-center text-dark bg-white border-0 shadow-lg mb-2 rounded-3" role="alert" aria-live="assertive" aria-atomic="true">
                    <div class="d-flex">
                        <div class="toast-body p-3">
                            <div class="d-flex align-items-center gap-2 mb-1">
                                <span class="badge ${getTypeBadgeClass(item.type)}">${item.type}</span>
                                <strong class="small text-dark">${item.title}</strong>
                            </div>
                            <div class="text-secondary small text-truncate" style="max-width: 280px;">${item.content}</div>
                            <div class="mt-2 pt-1 border-top">
                                <a href="${item.targetUrl || '/Notifications'}" class="btn btn-sm btn-link text-primary p-0 text-decoration-none fw-semibold">
                                    Xem ngay <i class="bi bi-arrow-right"></i>
                                </a>
                            </div>
                        </div>
                        <button type="button" class="btn-close me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                </div>
            `;
            toastContainer.insertAdjacentHTML('beforeend', toastHtml);
            const toastEl = document.getElementById(toastId);
            if (toastEl && typeof bootstrap !== 'undefined' && bootstrap.Toast) {
                const toast = new bootstrap.Toast(toastEl, { delay: 6000 });
                toast.show();
                toastEl.addEventListener('hidden.bs.toast', function () {
                    toastEl.remove();
                });
            }
        }

        async function fetchRecentNotifications() {
            try {
                const response = await fetch('/api/notifications/recent', {
                    method: 'GET',
                    headers: { 'Accept': 'application/json' }
                });

                if (!response.ok) {
                    throw new Error('HTTP error ' + response.status);
                }

                const data = await response.json();
                renderNotifications(data);
                isLoaded = true;
            } catch (err) {
                if (notificationList) {
                    notificationList.innerHTML = `
                        <div class="p-3 text-center text-danger small">
                            <i class="bi bi-exclamation-triangle me-1"></i>
                            <span>Không thể tải thông báo. Vui lòng thử lại.</span>
                        </div>
                    `;
                }
            }
        }

        // Tải thông báo ban đầu
        fetchRecentNotifications();

        // Refresh khi mở dropdown
        notificationBellBtn.addEventListener('click', function () {
            if (!isLoaded) {
                fetchRecentNotifications();
            }
        });

        // Xử lý Đánh dấu tất cả đã đọc
        if (markAllReadBtn) {
            markAllReadBtn.addEventListener('click', async function (e) {
                e.stopPropagation();
                try {
                    const token = getCsrfToken();
                    const response = await fetch('/api/notifications/mark-all-read', {
                        method: 'POST',
                        headers: {
                            'RequestVerificationToken': token,
                            'Accept': 'application/json'
                        }
                    });
                    if (response.ok) {
                        updateBadge(0);
                        notificationList.querySelectorAll('.notification-item').forEach(el => {
                            el.classList.remove('bg-primary-subtle', 'bg-opacity-10');
                        });
                    }
                } catch (err) {
                    console.warn('Lỗi khi đánh dấu đã đọc:', err);
                }
            });
        }

        // ==========================================
        // SIGNALR REALTIME HUB CONNECTION
        // ==========================================
        if (typeof signalR !== 'undefined') {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl('/hubs/notifications')
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            connection.on('ReceiveNotification', function (item) {
                // Tăng số lượng chưa đọc
                updateBadge(currentUnreadCount + 1);

                // Chèn thông báo mới vào dropdown
                prependRealtimeNotification(item);

                // Hiển thị toast realtime
                showNotificationToast(item);
            });

            connection.onreconnected(function (connectionId) {
                console.log('SignalR reconnected:', connectionId);
                fetchRecentNotifications();
            });

            connection.start().catch(function (err) {
                console.warn('SignalR Realtime connection failed (will retry if configured):', err);
            });
        }
    }
});
