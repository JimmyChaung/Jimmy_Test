// 函數：點擊子項目後添加 active 樣式，並移除其他項目的 active 樣式
function activateItem(element) {
    // 移除其他項目的 active 樣式
    const items = document.querySelectorAll('.section-item');
    items.forEach(item => item.classList.remove('active'));

    // 為點擊的項目添加 active 樣式
    element.classList.add('active');
}

// Vue.js 實例
new Vue({
    el: '#app',
    data: {
        menuItems: [
            { name: 'Account_management', url: '/Permission/Account_management' },
            { name: 'Role_authority', url: '/Permission/Role_authority' },
            { name: 'Tool_management', url: '/Permission/Tool_management' }
        ],
        currentPageContent: '',   // 當前頁面內容
        selectedItem: ''          // 追蹤當前選中的項目名稱
    },
    methods: {
        // 載入選定的頁面並設置 active 樣式
        selectPage(item) {


            // 更新選中的項目名稱
            this.selectedItem = item.name;

            // 載入選定的頁面內容
            fetch(item.url)
                .then(response => {
                    if (!response.ok) {
                        throw new Error('Network response was not ok');
                    }
                    return response.text(); // 轉換為文本
                })
                .then(data => {
                    this.currentPageContent = data; // 更新當前頁面內容
                })
                .catch(error => {
                    console.error('Error loading page:', error);
                    alert("Error loading page: " + error.message);
                });


        }
    },
    mounted() {
        // 預設載入第一個頁面
        this.selectPage(this.menuItems[0]);
    }
});






function loadPage(page) {
    fetch(`/Permission/${page}`)
        .then(response => {
            if (!response.ok) throw new Error('Network response was not ok');
            return response.text();
        })
        .then(html => {
            document.getElementById('content').innerHTML = html; // 更新內容區域
            if (page === 'Account_management') {
                initializeAccountManagement(); // 呼叫初始化函數
            }
        })
        .catch(error => {
            console.error('Error loading page:', error);
            document.getElementById('content').innerHTML = '<p>載入失敗，請稍後再試。</p>';
        });
}

// 專門為帳戶管理頁面初始化的函數
function initializeAccountManagement() {
    // 在這裡添加與帳戶管理相關的初始化代碼
    console.log('帳戶管理頁面初始化！');
}








// 專門為帳戶管理頁面初始化的函數
function initializeAccountManagement() {
    // 在這裡添加與帳戶管理相關的初始化代碼
    console.log('帳戶管理頁面初始化！');
}