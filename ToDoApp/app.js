(() => {
  const STORAGE_KEY = 'simple-todos-v1';

  /** @type {{id:string,title:string,completed:boolean,createdAt:number}[]} */
  let todos = load();
  let currentFilter = 'all'; // all | active | completed

  // Elements
  const form = document.getElementById('todo-form');
  const input = document.getElementById('new-todo');
  const list = document.getElementById('todo-list');
  const itemsLeftEl = document.getElementById('items-left');
  const filterButtons = Array.from(document.querySelectorAll('.filter'));
  const clearBtn = document.getElementById('clear-completed');

  // Initial render
  render();

  // Events
  form.addEventListener('submit', (e) => {
    e.preventDefault();
    const title = (input.value || '').trim();
    if (!title) return;
    addTodo(title);
    input.value = '';
  });

  filterButtons.forEach((btn) =>
    btn.addEventListener('click', () => setFilter(btn.dataset.filter))
  );

  clearBtn.addEventListener('click', () => {
    const hadCompleted = todos.some(t => t.completed);
    todos = todos.filter(t => !t.completed);
    if (hadCompleted) save();
    render();
  });

  // Event delegation for list actions
  list.addEventListener('click', (e) => {
    const target = /** @type {HTMLElement} */(e.target);
    const itemEl = target.closest('[data-id]');
    if (!itemEl) return;
    const id = itemEl.getAttribute('data-id');

    if (target.matches('button.delete')) {
      deleteTodo(id);
    } else if (target.matches('button.edit')) {
      startEdit(id);
    }
  });

  list.addEventListener('change', (e) => {
    const target = /** @type {HTMLElement} */(e.target);
    if (target.matches('input.toggle')) {
      const itemEl = target.closest('[data-id]');
      if (!itemEl) return;
      const id = itemEl.getAttribute('data-id');
      toggleTodo(id, /** @type {HTMLInputElement} */(target).checked);
    }
  });

  list.addEventListener('dblclick', (e) => {
    const target = /** @type {HTMLElement} */(e.target);
    const titleEl = target.closest('.title');
    if (!titleEl) return;
    const itemEl = titleEl.closest('[data-id]');
    if (!itemEl) return;
    const id = itemEl.getAttribute('data-id');
    startEdit(id);
  });

  function addTodo(title) {
    const todo = {
      id: cryptoRandomId(),
      title,
      completed: false,
      createdAt: Date.now(),
    };
    todos.unshift(todo);
    save();
    render();
  }

  function toggleTodo(id, completed) {
    const t = todos.find(t => t.id === id);
    if (!t) return;
    t.completed = completed;
    save();
    render();
  }

  function deleteTodo(id) {
    const before = todos.length;
    todos = todos.filter(t => t.id !== id);
    if (todos.length !== before) {
      save();
      render();
    }
  }

  function startEdit(id) {
    const t = todos.find(t => t.id === id);
    if (!t) return;
    // Replace title with an input
    const li = list.querySelector(`[data-id="${CSS.escape(id)}"]`);
    if (!li) return;
    const titleEl = li.querySelector('.title');
    if (!titleEl) return;
    const inputEl = document.createElement('input');
    inputEl.type = 'text';
    inputEl.value = t.title;
    inputEl.setAttribute('aria-label', 'Todoタイトルの編集');
    titleEl.replaceWith(inputEl);
    inputEl.focus();
    inputEl.select();

    const finish = (commit) => {
      const newTitle = (inputEl.value || '').trim();
      if (commit && newTitle) {
        t.title = newTitle;
        save();
      }
      render();
    };

    inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') finish(true);
      if (e.key === 'Escape') finish(false);
    });
    inputEl.addEventListener('blur', () => finish(true));
  }

  function setFilter(f) {
    currentFilter = f;
    filterButtons.forEach((btn) => {
      const isActive = btn.dataset.filter === f;
      btn.classList.toggle('is-active', isActive);
      btn.setAttribute('aria-selected', String(isActive));
    });
    render();
  }

  function filteredTodos() {
    if (currentFilter === 'active') return todos.filter(t => !t.completed);
    if (currentFilter === 'completed') return todos.filter(t => t.completed);
    return todos;
  }

  function itemsLeft() {
    return todos.filter(t => !t.completed).length;
  }

  function render() {
    const items = filteredTodos();
    list.innerHTML = items.map(viewItem).join('');
    itemsLeftEl.textContent = `${itemsLeft()} 件の未完了`;
    clearBtn.disabled = !todos.some(t => t.completed);
  }

  function viewItem(t) {
    const cls = ['todo', t.completed ? 'completed' : ''].filter(Boolean).join(' ');
    return `
      <li class="${cls}" data-id="${escapeHtml(t.id)}">
        <input class="toggle" type="checkbox" ${t.completed ? 'checked' : ''} aria-label="完了切り替え" />
        <span class="title">${escapeHtml(t.title)}</span>
        <span class="actions">
          <button class="edit" aria-label="編集">編集</button>
          <button class="delete" aria-label="削除">削除</button>
        </span>
      </li>
    `;
  }

  function save() {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(todos));
  }

  function load() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) return [];
      const arr = JSON.parse(raw);
      if (!Array.isArray(arr)) return [];
      return arr.filter(Boolean).map((t) => ({
        id: String(t.id || cryptoRandomId()),
        title: String(t.title || ''),
        completed: Boolean(t.completed),
        createdAt: Number(t.createdAt || Date.now()),
      }));
    } catch {
      return [];
    }
  }

  function escapeHtml(s) {
    return s
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  function cryptoRandomId() {
    if (window.crypto?.randomUUID) return window.crypto.randomUUID();
    // Fallback: timestamp + random
    return 'id-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 8);
  }
})();

