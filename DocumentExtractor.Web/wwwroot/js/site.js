// Desktop Application JavaScript

// Update clock in status bar
function updateClock() {
    const now = new Date();
    const timeString = now.toLocaleTimeString('en-US', { 
        hour12: false,
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });
    
    const clockElement = document.getElementById('current-time');
    if (clockElement) {
        clockElement.textContent = timeString;
    }
}

// Initialize clock when page loads
document.addEventListener('DOMContentLoaded', function() {
    updateClock();
    setInterval(updateClock, 1000);
    
    // Delete document function
    window.deleteDocument = function(id, fileName) {
        const deleteModal = document.getElementById('deleteModal');
        if (deleteModal) {
            document.getElementById('deleteDocumentId').value = id;
            document.getElementById('deleteFileName').textContent = fileName;
            new bootstrap.Modal(deleteModal).show();
        }
    };
});
