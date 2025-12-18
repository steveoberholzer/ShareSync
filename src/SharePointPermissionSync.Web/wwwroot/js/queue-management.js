// Queue Management JavaScript
// Handles item details viewing, retry, and delete operations

// Load statistics on page load
document.addEventListener('DOMContentLoaded', function () {
    refreshStatistics();
    // Auto-refresh statistics every 30 seconds
    setInterval(refreshStatistics, 30000);
});

/**
 * Refresh queue statistics cards
 */
function refreshStatistics() {
    fetch('/Queue/Statistics')
        .then(response => response.json())
        .then(data => {
            document.getElementById('stat-pending').textContent = data.pending;
            document.getElementById('stat-processing').textContent = data.processing;
            document.getElementById('stat-completed').textContent = data.completed;
            document.getElementById('stat-failed').textContent = data.failed;
        })
        .catch(error => {
            console.error('Error loading statistics:', error);
        });
}

/**
 * View item details in modal
 */
function viewItemDetails(messageId) {
    const modal = new bootstrap.Modal(document.getElementById('itemDetailsModal'));
    const contentDiv = document.getElementById('itemDetailsContent');

    // Show loading spinner
    contentDiv.innerHTML = `
        <div class="text-center">
            <div class="spinner-border" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>
    `;

    modal.show();

    // Fetch item details
    fetch(`/Queue/ItemDetails?messageId=${messageId}`)
        .then(response => {
            if (!response.ok) {
                throw new Error('Failed to load item details');
            }
            return response.json();
        })
        .then(data => {
            const statusClass = getStatusClass(data.status);
            const createdDate = new Date(data.createdAt).toLocaleString();
            const processedDate = data.processedAt ? new Date(data.processedAt).toLocaleString() : 'N/A';

            contentDiv.innerHTML = `
                <div class="row mb-3">
                    <div class="col-md-6">
                        <h6 class="text-muted">Message ID</h6>
                        <p class="font-monospace">${data.messageId}</p>
                    </div>
                    <div class="col-md-6">
                        <h6 class="text-muted">Job ID</h6>
                        <p>
                            <a href="/Jobs/Details/${data.jobId}" class="font-monospace">
                                ${data.jobId}
                            </a>
                        </p>
                    </div>
                </div>
                <div class="row mb-3">
                    <div class="col-md-6">
                        <h6 class="text-muted">Item Type</h6>
                        <p>${data.itemType || 'Unknown'}</p>
                    </div>
                    <div class="col-md-6">
                        <h6 class="text-muted">Identifier</h6>
                        <p>${data.itemIdentifier || 'N/A'}</p>
                    </div>
                </div>
                <div class="row mb-3">
                    <div class="col-md-6">
                        <h6 class="text-muted">Status</h6>
                        <p><span class="badge bg-${statusClass}">${data.status}</span></p>
                    </div>
                    <div class="col-md-6">
                        <h6 class="text-muted">Retry Count</h6>
                        <p>${data.retryCount} / ${data.maxRetries}</p>
                    </div>
                </div>
                <div class="row mb-3">
                    <div class="col-md-6">
                        <h6 class="text-muted">Created At</h6>
                        <p>${createdDate}</p>
                    </div>
                    <div class="col-md-6">
                        <h6 class="text-muted">Processed At</h6>
                        <p>${processedDate}</p>
                    </div>
                </div>
                ${data.errorMessage ? `
                    <div class="row mb-3">
                        <div class="col-md-12">
                            <h6 class="text-muted">Error Message</h6>
                            <div class="alert alert-danger">
                                <pre class="mb-0" style="white-space: pre-wrap;">${escapeHtml(data.errorMessage)}</pre>
                            </div>
                        </div>
                    </div>
                ` : ''}
                <div class="row">
                    <div class="col-md-12">
                        <h6 class="text-muted">Message Payload</h6>
                        <pre class="bg-light p-3 rounded" style="max-height: 300px; overflow-y: auto;"><code>${escapeHtml(data.payload)}</code></pre>
                    </div>
                </div>
            `;
        })
        .catch(error => {
            contentDiv.innerHTML = `
                <div class="alert alert-danger">
                    <i class="bi bi-exclamation-triangle"></i> ${error.message}
                </div>
            `;
        });
}

/**
 * Retry a failed item
 */
function retryItem(messageId) {
    if (!confirm('Are you sure you want to retry this item? It will be added back to the queue for processing.')) {
        return;
    }

    // Show loading indicator
    const button = event.target.closest('button');
    const originalHtml = button.innerHTML;
    button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span>';
    button.disabled = true;

    fetch('/Queue/RetryItem', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(messageId)
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showAlert('success', data.message);
                // Reload page after 1 second
                setTimeout(() => location.reload(), 1000);
            } else {
                showAlert('danger', data.message);
                button.innerHTML = originalHtml;
                button.disabled = false;
            }
        })
        .catch(error => {
            showAlert('danger', 'Error retrying item: ' + error.message);
            button.innerHTML = originalHtml;
            button.disabled = false;
        });
}

/**
 * Delete an item
 */
function deleteItem(messageId) {
    if (!confirm('Are you sure you want to delete this item? This action cannot be undone.')) {
        return;
    }

    // Show loading indicator
    const button = event.target.closest('button');
    const originalHtml = button.innerHTML;
    button.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span>';
    button.disabled = true;

    fetch('/Queue/DeleteItem', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(messageId)
    })
        .then(response => response.json())
        .then(data => {
            if (data.success) {
                showAlert('success', data.message);
                // Reload page after 1 second
                setTimeout(() => location.reload(), 1000);
            } else {
                showAlert('danger', data.message);
                button.innerHTML = originalHtml;
                button.disabled = false;
            }
        })
        .catch(error => {
            showAlert('danger', 'Error deleting item: ' + error.message);
            button.innerHTML = originalHtml;
            button.disabled = false;
        });
}

/**
 * Show an alert message
 */
function showAlert(type, message) {
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} alert-dismissible fade show position-fixed top-0 start-50 translate-middle-x mt-3`;
    alertDiv.style.zIndex = '9999';
    alertDiv.innerHTML = `
        ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
    `;
    document.body.appendChild(alertDiv);

    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        alertDiv.remove();
    }, 5000);
}

/**
 * Get Bootstrap class for status badge
 */
function getStatusClass(status) {
    switch (status) {
        case 'Completed':
            return 'success';
        case 'Failed':
            return 'danger';
        case 'Processing':
            return 'info';
        case 'Pending':
            return 'warning';
        default:
            return 'secondary';
    }
}

/**
 * Escape HTML to prevent XSS
 */
function escapeHtml(text) {
    const map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, m => map[m]);
}
