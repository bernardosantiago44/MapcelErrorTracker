(() => {
    const dropdownSelector = '[data-status-dropdown]';
    const triggerSelector = '[data-status-trigger]';
    const optionSelector = '[data-status-option]';

    let openDropdown = null;
    let pointerOpenedDropdown = null;

    function getParts(dropdown) {
        return {
            form: dropdown.closest('[data-status-dropdown-form]'),
            trigger: dropdown.querySelector(triggerSelector),
            menu: dropdown.querySelector('[data-status-menu]'),
            valueInput: dropdown.closest('[data-status-dropdown-form]')?.querySelector('[data-status-value]'),
            label: dropdown.querySelector('[data-status-label]')
        };
    }

    function positionMenu(dropdown) {
        const { trigger, menu } = getParts(dropdown);
        if (!trigger || !menu) return;

        const rect = trigger.getBoundingClientRect();
        const menuWidth = Math.max(rect.width, 150);
        const menuHeight = menu.offsetHeight || 150;
        const spaceBelow = window.innerHeight - rect.bottom;
        const opensUp = spaceBelow < menuHeight + 8 && rect.top > menuHeight;
        const top = opensUp ? rect.top - menuHeight - 4 : rect.bottom + 4;
        const left = Math.min(rect.left, window.innerWidth - menuWidth - 8);

        menu.style.minWidth = `${menuWidth}px`;
        menu.style.left = `${Math.max(8, left)}px`;
        menu.style.top = `${Math.max(8, top)}px`;
    }

    function closeDropdown(dropdown) {
        if (!dropdown) return;

        const { trigger } = getParts(dropdown);
        dropdown.classList.remove('is-open');
        trigger?.setAttribute('aria-expanded', 'false');

        if (openDropdown === dropdown) {
            openDropdown = null;
        }
    }

    function closeOpenDropdown(exceptDropdown = null) {
        if (openDropdown && openDropdown !== exceptDropdown) {
            closeDropdown(openDropdown);
        }
    }

    function openStatusDropdown(dropdown) {
        closeOpenDropdown(dropdown);

        const { trigger } = getParts(dropdown);
        dropdown.classList.add('is-open');
        trigger?.setAttribute('aria-expanded', 'true');
        openDropdown = dropdown;
        positionMenu(dropdown);
    }

    function setActiveOption(dropdown, option) {
        dropdown.querySelectorAll(optionSelector).forEach((item) => {
            item.classList.toggle('is-active', item === option);
        });
    }

    function selectedOption(dropdown) {
        return dropdown.querySelector(`${optionSelector}[aria-selected="true"]`);
    }

    function refreshReturnUrl(form) {
        const returnUrlInput = form?.querySelector('input[name="returnUrl"]');
        if (!returnUrlInput) return;

        returnUrlInput.value = `${window.location.pathname}${window.location.search}`;
    }

    function submitForm(form) {
        refreshReturnUrl(form);

        if (form.requestSubmit) {
            form.requestSubmit();
            return;
        }

        form.submit();
    }

    function chooseOption(dropdown, option) {
        if (!dropdown || !option) return;

        const { form, valueInput, label } = getParts(dropdown);
        const previousValue = valueInput?.value;
        const nextValue = option.dataset.value;
        const nextLabel = option.dataset.label;

        closeDropdown(dropdown);

        if (!valueInput || !form || !nextValue || previousValue === nextValue) {
            return;
        }

        valueInput.value = nextValue;
        if (label && nextLabel) label.textContent = nextLabel;

        dropdown.querySelectorAll(optionSelector).forEach((item) => {
            const isSelected = item === option;
            item.classList.toggle('is-selected', isSelected);
            item.setAttribute('aria-selected', isSelected ? 'true' : 'false');
        });

        submitForm(form);
    }

    function optionFromPoint(event, dropdown) {
        const target = document.elementFromPoint(event.clientX, event.clientY);
        const option = target?.closest(optionSelector);
        return option && dropdown.contains(option) ? option : null;
    }

    document.addEventListener('pointerdown', (event) => {
        const trigger = event.target.closest(triggerSelector);
        if (trigger) {
            if (event.button !== 0) return;

            event.preventDefault();
            const dropdown = trigger.closest(dropdownSelector);
            openStatusDropdown(dropdown);
            setActiveOption(dropdown, selectedOption(dropdown));
            pointerOpenedDropdown = dropdown;
            trigger.focus();
            return;
        }

        if (!event.target.closest(dropdownSelector)) {
            closeOpenDropdown();
        }
    });

    document.addEventListener('pointerup', (event) => {
        if (!pointerOpenedDropdown) return;

        const dropdown = pointerOpenedDropdown;
        pointerOpenedDropdown = null;
        const option = optionFromPoint(event, dropdown);

        if (option) {
            event.preventDefault();
            chooseOption(dropdown, option);
        }
    });

    document.addEventListener('click', (event) => {
        const option = event.target.closest(optionSelector);
        if (!option) return;

        event.preventDefault();
        chooseOption(option.closest(dropdownSelector), option);
    });

    document.addEventListener('pointerover', (event) => {
        const option = event.target.closest(optionSelector);
        if (!option) return;

        const dropdown = option.closest(dropdownSelector);
        if (dropdown?.classList.contains('is-open')) {
            setActiveOption(dropdown, option);
        }
    });

    document.addEventListener('keydown', (event) => {
        const trigger = event.target.closest(triggerSelector);
        if (!trigger) return;

        const dropdown = trigger.closest(dropdownSelector);
        const isOpen = dropdown.classList.contains('is-open');

        if (event.key === 'Escape') {
            closeDropdown(dropdown);
            return;
        }

        if (!isOpen && ['Enter', ' ', 'ArrowDown', 'ArrowUp'].includes(event.key)) {
            event.preventDefault();
            openStatusDropdown(dropdown);
            setActiveOption(dropdown, selectedOption(dropdown));
            return;
        }

        if (!isOpen) return;

        const options = [...dropdown.querySelectorAll(optionSelector)];
        const current = dropdown.querySelector(`${optionSelector}.is-active`) || selectedOption(dropdown);
        const currentIndex = Math.max(0, options.indexOf(current));

        if (event.key === 'Enter' || event.key === ' ') {
            event.preventDefault();
            chooseOption(dropdown, current);
        }

        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
            event.preventDefault();
            const direction = event.key === 'ArrowDown' ? 1 : -1;
            const nextIndex = (currentIndex + direction + options.length) % options.length;
            setActiveOption(dropdown, options[nextIndex]);
        }
    });

    document.addEventListener('submit', (event) => {
        const form = event.target.closest('[data-status-dropdown-form]');
        if (form) {
            refreshReturnUrl(form);
        }
    });

    window.addEventListener('resize', () => closeOpenDropdown());
    window.addEventListener('scroll', () => closeOpenDropdown(), true);
})();
