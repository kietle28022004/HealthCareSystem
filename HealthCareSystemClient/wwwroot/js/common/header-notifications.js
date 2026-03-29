(function () {
    const config = window.HeaderNotificationConfig || {};
    const btn = document.getElementById('headerNotificationBtn');
    const panel = document.getElementById('headerNotificationPanel');
    const list = document.getElementById('headerNotificationList');
    const badge = document.getElementById('headerNotificationBadge');
    const updatedAt = document.getElementById('headerNotificationUpdatedAt');

    if (!btn || !panel || !list || !badge) {
        return;
    }

    function buildApiUrl(path) {
        const base = config.apiBaseUrl || '';
        if (!base) return path;
        return path.startsWith('/') ? `${base}${path}` : `${base}/${path}`;
    }

    function authHeaders() {
        const headers = { Accept: 'application/json' };
        if (config.userToken) {
            headers.Authorization = `Bearer ${config.userToken}`;
        }
        return headers;
    }

    function parseDate(value) {
        if (!value) return null;
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? null : d;
    }

    function formatRelativeTime(date) {
        if (!date) return '';
        const diffMs = Date.now() - date.getTime();
        const diffMin = Math.floor(diffMs / 60000);
        if (diffMin < 1) return 'just now';
        if (diffMin < 60) return `${diffMin}m ago`;
        const diffHour = Math.floor(diffMin / 60);
        if (diffHour < 24) return `${diffHour}h ago`;
        const diffDay = Math.floor(diffHour / 24);
        return `${diffDay}d ago`;
    }

    function normalizeStatus(status) {
        return String(status || '').toLowerCase();
    }

    function isUpcomingAppointment(appointmentDate, status) {
        if (!appointmentDate) return false;
        const normalized = normalizeStatus(status);
        if (normalized === 'completed' || normalized === 'cancelled') return false;

        const now = new Date();
        const horizon = new Date(now);
        horizon.setDate(horizon.getDate() + 3);
        return appointmentDate >= now && appointmentDate <= horizon;
    }

    function renderNotifications(items) {
        if (!Array.isArray(items) || items.length === 0) {
            list.innerHTML = '<div class="list-group-item text-muted">No new notifications</div>';
            badge.style.display = 'none';
            badge.textContent = '0';
            return;
        }

        // Badge: CHỈ hiện cho tin nhắn chưa đọc, KHÔNG tính appointment
        const unreadMessages = items.filter(i => i.type === 'message' && Number(i.count || 0) > 0);
        const unreadCount = unreadMessages.reduce((sum, item) => sum + Number(item.count || 0), 0);
        if (unreadCount > 0) {
            badge.textContent = String(unreadCount > 99 ? '99+' : unreadCount);
            badge.style.display = 'inline-flex';
        } else {
            badge.style.display = 'none';
            badge.textContent = '0';
        }

        list.innerHTML = items.map((item) => {
            const icon = item.type === 'message' ? 'fa-envelope' : 'fa-calendar-check';
            return `
                <button type="button" class="list-group-item list-group-item-action" data-target-url="${item.targetUrl}">
                    <div class="d-flex w-100 justify-content-between">
                        <h6 class="mb-1"><i class="fas ${icon} me-2"></i>${item.title}</h6>
                        <small>${item.timeLabel}</small>
                    </div>
                    <p class="mb-1">${item.message}</p>
                </button>
            `;
        }).join('');

        list.querySelectorAll('[data-target-url]').forEach((el) => {
            el.addEventListener('click', async () => {
                const targetUrl = el.getAttribute('data-target-url');

                // Mark all conversations as read
                await fetch(buildApiUrl('/api/conversation/mark-all-read'), {
                    method: 'POST',
                    headers: authHeaders()
                }).catch(() => {});

                // Hide badge immediately
                badge.style.display = 'none';
                badge.textContent = '0';

                if (targetUrl) {
                    window.location.href = targetUrl;
                }
            });
        });
    }

    async function fetchNotifications() {
        if (!config.userId || !config.role || !config.userToken) {
            renderNotifications([]);
            return;
        }

        const appointmentPath = String(config.role).toLowerCase() === 'doctor'
            ? `/api/Appointment/doctor/${config.userId}`
            : `/api/Appointment/patient/${config.userId}`;

        try {
            const [appointmentsRes, conversationsRes] = await Promise.all([
                fetch(buildApiUrl(appointmentPath), { headers: authHeaders() }),
                fetch(buildApiUrl('/api/conversation/my-conversations'), { headers: authHeaders() })
            ]);

            const appointments = appointmentsRes.ok ? await appointmentsRes.json() : [];
            const conversations = conversationsRes.ok ? await conversationsRes.json() : [];

            const appointmentItems = (Array.isArray(appointments) ? appointments : [])
                .map((a) => {
                    const when = parseDate(a.appointmentDateTime || a.AppointmentDateTime);
                    const status = a.status || a.Status;
                    const counterpart = String(config.role).toLowerCase() === 'doctor'
                        ? (a.patientName || a.PatientName || 'Patient')
                        : (a.doctorName || a.DoctorName || 'Doctor');

                    if (!isUpcomingAppointment(when, status)) {
                        return null;
                    }

                    return {
                        type: 'appointment',
                        title: 'Upcoming appointment',
                        message: `${counterpart} at ${when ? when.toLocaleString() : 'scheduled time'}`,
                        date: when || new Date(),
                        timeLabel: formatRelativeTime(when),
                        targetUrl: String(config.role).toLowerCase() === 'doctor' ? '/Doctor/Appointments' : '/User/Appointments',
                        count: 1
                    };
                })
                .filter(Boolean);

            const messageItems = (Array.isArray(conversations) ? conversations : [])
                .filter((c) => Number(c.unreadCount || 0) > 0)
                .map((c) => {
                    const unread = Number(c.unreadCount || 0);
                    const sentAt = parseDate(c.lastMessage?.sentAt || c.updatedAt);
                    return {
                        type: 'message',
                        title: 'New message',
                        message: `${c.otherUserName || 'User'} sent ${unread} unread message${unread > 1 ? 's' : ''}`,
                        date: sentAt || new Date(),
                        timeLabel: formatRelativeTime(sentAt),
                        targetUrl: String(config.role).toLowerCase() === 'doctor' ? '/Doctor/Messages' : '/User/Messages',
                        count: unread
                    };
                });

            const allItems = [...appointmentItems, ...messageItems]
                .sort((a, b) => (b.date?.getTime() || 0) - (a.date?.getTime() || 0))
                .slice(0, 20);

            renderNotifications(allItems);
            if (updatedAt) {
                updatedAt.textContent = new Date().toLocaleTimeString();
            }
        } catch (error) {
            console.error('Failed to fetch header notifications:', error);
            list.innerHTML = '<div class="list-group-item text-danger">Failed to load notifications</div>';
        }
    }

    function togglePanel(show) {
        panel.style.display = show ? 'block' : 'none';
        btn.setAttribute('aria-expanded', show ? 'true' : 'false');
    }

    btn.addEventListener('click', async (event) => {
        event.stopPropagation();
        const willShow = panel.style.display !== 'block';
        togglePanel(willShow);
        if (willShow) {
            // Mark all as read first, then fetch fresh notifications
            await fetch(buildApiUrl('/api/conversation/mark-all-read'), {
                method: 'POST',
                headers: authHeaders()
            }).catch(() => {});
            // Hide badge immediately after mark-as-read completes
            badge.style.display = 'none';
            badge.textContent = '0';
            await fetchNotifications();
        }
    });

    document.addEventListener('click', (event) => {
        if (!panel.contains(event.target) && !btn.contains(event.target)) {
            togglePanel(false);
        }
    });

    fetchNotifications();
    window.setInterval(fetchNotifications, 60000);
})();
