// Doctor Schedule JavaScript (API-driven)

function getDoctorId() {
    const raw = document.body.getAttribute('data-doctor-id') || '0';
    return Number.parseInt(raw, 10) || 0;
}

function getCurrentWeekDateFromUrl() {
    const params = new URLSearchParams(window.location.search);
    const week = params.get('week');
    if (!week) return new Date();
    const parsed = new Date(week);
    return Number.isNaN(parsed.getTime()) ? new Date() : parsed;
}

function getStartOfWeek(date) {
    const d = new Date(date);
    const day = d.getDay();
    d.setDate(d.getDate() - day);
    d.setHours(0, 0, 0, 0);
    return d;
}

function navigateWeek(offsetDays) {
    const current = getCurrentWeekDateFromUrl();
    current.setDate(current.getDate() + offsetDays);
    const yyyy = current.getFullYear();
    const mm = String(current.getMonth() + 1).padStart(2, '0');
    const dd = String(current.getDate()).padStart(2, '0');
    window.location.href = `/Doctor/Schedule?week=${yyyy}-${mm}-${dd}`;
}

function previousWeek() {
    navigateWeek(-7);
}

function nextWeek() {
    navigateWeek(7);
}

function addTimeOff() {
    document.getElementById('timeOffId').value = '0';
    document.getElementById('timeOffType').value = 'vacation';
    document.getElementById('timeOffTitle').value = '';
    document.getElementById('startDate').value = '';
    document.getElementById('endDate').value = '';
    document.getElementById('allDay').checked = true;
    document.getElementById('timeOffReason').value = '';

    const saveBtn = document.getElementById('saveTimeOffBtn');
    if (saveBtn) saveBtn.textContent = 'Add Time Off';

    const modal = new bootstrap.Modal(document.getElementById('addTimeOffModal'));
    modal.show();
}

function closeTimeOffModal() {
    const modalEl = document.getElementById('addTimeOffModal');
    const modal = bootstrap.Modal.getInstance(modalEl);
    if (modal) modal.hide();
}

async function saveTimeOff() {
    const doctorId = getDoctorId();
    if (!doctorId) {
        alert('Cannot determine doctor ID. Please login again.');
        return;
    }

    const timeOffId = Number.parseInt(document.getElementById('timeOffId')?.value || '0', 10);
    const type = document.getElementById('timeOffType')?.value;
    const title = document.getElementById('timeOffTitle')?.value?.trim();
    const startDate = document.getElementById('startDate')?.value;
    const endDate = document.getElementById('endDate')?.value;
    const isAllDay = document.getElementById('allDay')?.checked ?? true;
    const reason = document.getElementById('timeOffReason')?.value?.trim();

    if (!title || !startDate || !endDate) {
        alert('Please fill title, start date, and end date.');
        return;
    }

    const payload = {
        doctorUserId: doctorId,
        type: type || 'personal',
        title,
        startDate,
        endDate,
        isAllDay,
        reason: reason || ''
    };

    try {
        const isEdit = timeOffId > 0;
        const url = isEdit
            ? `https://localhost:7293/api/Appointment/TimeOff/${timeOffId}`
            : 'https://localhost:7293/api/Appointment/TimeOff';

        const res = await fetch(url, {
            method: isEdit ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!res.ok) {
            const err = await res.text();
            throw new Error(err || `Failed to ${isEdit ? 'update' : 'create'} time off`);
        }

        closeTimeOffModal();
        window.location.reload();
    } catch (error) {
        console.error('saveTimeOff error:', error);
        alert('Failed to save time off: ' + error.message);
    }
}

async function deleteTimeOff(timeOffId) {
    const doctorId = getDoctorId();
    if (!doctorId) {
        alert('Cannot determine doctor ID. Please login again.');
        return;
    }

    if (!confirm('Are you sure you want to delete this time off?')) return;

    try {
        const res = await fetch(`https://localhost:7293/api/Appointment/TimeOff/${timeOffId}?doctorId=${doctorId}`, {
            method: 'DELETE'
        });

        if (!res.ok && res.status !== 204) {
            const err = await res.text();
            throw new Error(err || 'Failed to delete time off');
        }

        window.location.reload();
    } catch (error) {
        console.error('deleteTimeOff error:', error);
        alert('Failed to delete time off: ' + error.message);
    }
}

function editTimeOffFromButton(btn) {
    const timeOffId = btn.getAttribute('data-timeoff-id') || '0';
    const type = btn.getAttribute('data-type') || 'personal';
    const title = btn.getAttribute('data-title') || '';
    const startDate = btn.getAttribute('data-start-date') || '';
    const endDate = btn.getAttribute('data-end-date') || '';
    const reason = btn.getAttribute('data-reason') || '';

    document.getElementById('timeOffId').value = timeOffId;
    document.getElementById('timeOffType').value = type;
    document.getElementById('timeOffTitle').value = title;
    document.getElementById('startDate').value = startDate;
    document.getElementById('endDate').value = endDate;
    document.getElementById('allDay').checked = true;
    document.getElementById('timeOffReason').value = reason;

    const saveBtn = document.getElementById('saveTimeOffBtn');
    if (saveBtn) saveBtn.textContent = 'Update Time Off';

    const modal = new bootstrap.Modal(document.getElementById('addTimeOffModal'));
    modal.show();
}
