// Initialize tooltips
var tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'))
var tooltipList = tooltipTriggerList.map(function (tooltipTriggerEl) {
    return new bootstrap.Tooltip(tooltipTriggerEl)
})

// Form validation and modal handling
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOM loaded - initializing TasteShare scripts');

    // Test if modals exist
    const registerModal = document.getElementById('registerModal');
    const loginModal = document.getElementById('loginModal');

    console.log('Register modal found:', !!registerModal);
    console.log('Login modal found:', !!loginModal);

    // Handle login form submission - REMOVE preventDefault() to allow normal form submission
    const loginForm = document.getElementById('loginForm');
    if (loginForm) {
        loginForm.addEventListener('submit', function (e) {
            console.log('Login form submitted - allowing normal form submission');
            // Remove e.preventDefault() to allow the form to submit to your ASP.NET controller
            // You can add loading indicators here if needed

            // Optional: Add loading state
            const submitBtn = this.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Signing In...';
                submitBtn.disabled = true;
            }
        });
    }

    // Handle register form submission - REMOVE preventDefault() to allow normal form submission
    const registerForm = document.getElementById('registerForm');
    if (registerForm) {
        registerForm.addEventListener('submit', function (e) {
            console.log('Register form submitted - allowing normal form submission');
            // Remove e.preventDefault() to allow the form to submit to your ASP.NET controller

            // Client-side validation (optional - your server-side validation will also handle this)
            const password = document.querySelector('input[name="PasswordHash"]')?.value;
            const confirmPassword = document.getElementById('ConfirmPassword')?.value;

            if (password && confirmPassword && password !== confirmPassword) {
                e.preventDefault(); // Only prevent if passwords don't match
                alert('Passwords do not match!');
                return;
            }

            // Optional: Add loading state
            const submitBtn = this.querySelector('button[type="submit"]');
            if (submitBtn) {
                submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Creating Account...';
                submitBtn.disabled = true;
            }
        });
    }

    // Add click event listeners to modal triggers for debugging
    const joinButtons = document.querySelectorAll('[data-bs-target="#registerModal"]');
    joinButtons.forEach(button => {
        button.addEventListener('click', function () {
            console.log('Join Now button clicked');
        });
    });

    const loginButtons = document.querySelectorAll('[data-bs-target="#loginModal"]');
    loginButtons.forEach(button => {
        button.addEventListener('click', function () {
            console.log('Login button clicked');
        });
    });

    // Clear form fields and validation messages when modal is hidden
    $('#loginModal, #registerModal').on('hidden.bs.modal', function () {
        console.log('Modal hidden - clearing forms');

        // Clear form fields
        $(this).find('form')[0].reset();

        // Clear validation messages
        $(this).find('.text-danger').text('');
        $(this).find('.is-invalid').removeClass('is-invalid');

        // Reset submit buttons to original state
        const submitBtn = $(this).find('button[type="submit"]');
        if (submitBtn.length) {
            if ($(this).attr('id') === 'loginModal') {
                submitBtn.html('Sign In');
            } else {
                submitBtn.html('Create Account');
            }
            submitBtn.prop('disabled', false);
        }
    });

    // Show validation feedback on form inputs
    document.querySelectorAll('#registerForm input, #loginForm input').forEach(input => {
        input.addEventListener('blur', function () {
            if (this.value.trim() === '') {
                this.classList.add('is-invalid');
            } else {
                this.classList.remove('is-invalid');
                this.classList.add('is-valid');
            }
        });

        input.addEventListener('input', function () {
            if (this.value.trim() !== '') {
                this.classList.remove('is-invalid');
                this.classList.add('is-valid');
            }
        });
    });

    // Real-time password confirmation validation
    const confirmPasswordInput = document.getElementById('ConfirmPassword');
    const passwordInput = document.querySelector('input[name="PasswordHash"]');

    if (confirmPasswordInput && passwordInput) {
        confirmPasswordInput.addEventListener('input', function () {
            if (passwordInput.value !== this.value) {
                this.classList.add('is-invalid');
                this.classList.remove('is-valid');
            } else {
                this.classList.remove('is-invalid');
                this.classList.add('is-valid');
            }
        });

        passwordInput.addEventListener('input', function () {
            if (confirmPasswordInput.value && this.value !== confirmPasswordInput.value) {
                confirmPasswordInput.classList.add('is-invalid');
                confirmPasswordInput.classList.remove('is-valid');
            } else if (confirmPasswordInput.value) {
                confirmPasswordInput.classList.remove('is-invalid');
                confirmPasswordInput.classList.add('is-valid');
            }
        });
    }

    // Smooth scrolling for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const target = document.querySelector(this.getAttribute('href'));
            if (target) {
                target.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });

    // Auto-hide alerts after 5 seconds
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });
});

// Recipe filter functionality
function filterRecipes() {
    const form = document.querySelector('.filter-section form');
    if (form) {
        form.addEventListener('submit', function (e) {
            // Form will naturally submit and refresh with filters
            console.log('Recipe filters applied');
        });
    }
}

// Initialize when page loads
filterRecipes();

// Debug function to manually show modals
function showRegisterModal() {
    const modal = new bootstrap.Modal(document.getElementById('registerModal'));
    modal.show();
}

function showLoginModal() {
    const modal = new bootstrap.Modal(document.getElementById('loginModal'));
    modal.show();
}

// Utility function to show loading state on any form
function setFormLoading(form, isLoading) {
    const submitBtn = form.querySelector('button[type="submit"]');
    if (submitBtn) {
        if (isLoading) {
            submitBtn.setAttribute('data-original-text', submitBtn.innerHTML);
            submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-2"></i>Processing...';
            submitBtn.disabled = true;
        } else {
            const originalText = submitBtn.getAttribute('data-original-text');
            if (originalText) {
                submitBtn.innerHTML = originalText;
            }
            submitBtn.disabled = false;
        }
    }
}

// Handle successful form submissions (if you want to close modals programmatically)
function handleFormSuccess(modalId) {
    const modalElement = document.getElementById(modalId);
    if (modalElement) {
        const modal = bootstrap.Modal.getInstance(modalElement);
        if (modal) {
            modal.hide();
        }
    }
}

// Add this if you want to handle form submissions via AJAX in the future
/*
function submitFormViaAjax(form, successCallback) {
    const formData = new FormData(form);
    
    fetch(form.action, {
        method: 'POST',
        body: formData,
        headers: {
            'RequestVerificationToken': form.querySelector('input[name="__RequestVerificationToken"]').value
        }
    })
    .then(response => {
        if (response.redirected) {
            window.location.href = response.url;
        } else {
            return response.json();
        }
    })
    .then(data => {
        if (successCallback) successCallback(data);
    })
    .catch(error => {
        console.error('Error:', error);
        setFormLoading(form, false);
    });
}
*/